using System.Text.Json;
using Einvoicing.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Einvoicing.Tests.Knowledge;

public sealed class KnowledgeFeedbackStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"tuniflow-feedback-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task DisabledFeedback_DoesNotCreateStorage()
    {
        await CreateStore(enabled: false).RecordAsync("Private question", true, true, CancellationToken.None);

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task EnabledFeedback_DoesNotStoreRawQuestionsByDefault()
    {
        var store = CreateStore();
        await store.RecordAsync(" Private question ", true, null, CancellationToken.None);
        await store.RecordAsync("private question", false, false, CancellationToken.None);

        var records = ReadRecords();
        Assert.Equal(2, records.Length);
        Assert.All(records, row => Assert.Equal(JsonValueKind.Null, row.GetProperty("question").ValueKind));
        Assert.Equal(records[0].GetProperty("questionHash").GetString(), records[1].GetProperty("questionHash").GetString());
        Assert.True(records[0].GetProperty("found").GetBoolean());
        Assert.False(records[1].GetProperty("helpful").GetBoolean());
    }

    [Fact]
    public async Task OptInFeedback_LimitsQuestionLengthAndRemovesExpiredRecords()
    {
        var directory = Path.Combine(root, ".rag-feedback");
        Directory.CreateDirectory(directory);
        var expiredFile = Path.Combine(directory, "expired.jsonl");
        await File.WriteAllTextAsync(expiredFile, "{}");
        File.SetLastWriteTimeUtc(expiredFile, DateTime.UtcNow.AddDays(-31));

        await CreateStore(storeText: true).RecordAsync(new string('q', 1600), true, true, CancellationToken.None);

        Assert.False(File.Exists(expiredFile));
        Assert.Equal(new string('q', 1500), Assert.Single(ReadRecords()).GetProperty("question").GetString());
    }

    private KnowledgeFeedbackStore CreateStore(bool enabled = true, bool storeText = false)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(root);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Rag:FeedbackEnabled"] = enabled.ToString(),
            ["Rag:StoreQuestionText"] = storeText ? "true" : null
        }).Build();
        return new KnowledgeFeedbackStore(configuration, environment.Object);
    }

    private JsonElement[] ReadRecords()
        => File.ReadLines(Assert.Single(Directory.GetFiles(Path.Combine(root, ".rag-feedback"), "*.jsonl")))
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
