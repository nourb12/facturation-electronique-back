using System.Text;
using Einvoicing.Domain.Exceptions;
using Einvoicing.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Einvoicing.Tests.Demandes;

public sealed class KycFileStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"tuniflow-kyc-tests-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("document.PDF", "JVBERi0xLjcK")]
    [InlineData("scan.jpg", "/9j/AA==")]
    [InlineData("scan.jpeg", "/9j/AA==")]
    [InlineData("scan.png", "iVBORw0KGgo=")]
    [InlineData("scan.webp", "UklGRgAAAABXRUJQ")]
    public async Task ValidDocument_CanBeReadBack(string fileName, string content)
    {
        var bytes = Convert.FromBase64String(content);
        using var input = new MemoryStream(bytes);
        var service = Service(nameof(ValidDocument_CanBeReadBack));

        var relative = await service.SaveAsync(File(input, fileName), "kyc");
        await using var saved = await service.OpenReadAsync(relative);
        using var copy = new MemoryStream();
        await saved.CopyToAsync(copy);

        Assert.StartsWith("uploads/kyc/", relative);
        Assert.EndsWith(Path.GetExtension(fileName).ToLowerInvariant(), relative);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Theory]
    [InlineData("empty.pdf", "")]
    [InlineData("unsupported.txt", "%PDF-")]
    [InlineData("incorrect.png", "%PDF-")]
    [InlineData("protected.pdf", "%PDF-1.7\n/Encrypt")]
    public async Task InvalidDocument_IsRejectedBeforeWriting(string fileName, string content)
    {
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(content));
        await Assert.ThrowsAsync<ValidationMetierException>(() => Service().SaveAsync(File(input, fileName), "kyc"));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task OversizedDocument_IsRejectedBeforeWriting()
    {
        using var input = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
        await Assert.ThrowsAsync<ValidationMetierException>(() => Service().SaveAsync(File(input, "large.pdf"), "kyc"));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task MissingDocument_AndNonKycFolderAreRejected()
    {
        var service = Service();
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.OpenReadAsync("uploads/kyc/missing.pdf"));
        await Assert.ThrowsAsync<ValidationMetierException>(() => service.OpenReadAsync("uploads/misc/document.pdf"));
    }

    private FileStorageService Service(string? configuredFolder = null)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(root);
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Uploads:Root"] = configuredFolder }).Build();
        return new FileStorageService(env.Object, config);
    }

    private static IFormFile File(Stream stream, string fileName)
        => new FormFile(stream, 0, stream.Length, "document", fileName);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

