using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Einvoicing.Infrastructure.Services;

// Questions are stored verbatim only after an explicit opt-in. Default records contain no user ID or text.
public sealed class KnowledgeFeedbackStore(IConfiguration configuration,IWebHostEnvironment environment)
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private string Root=>Path.Combine(environment.ContentRootPath,".rag-feedback");
    public async Task RecordAsync(string question,bool found,bool? helpful,CancellationToken ct)
    {
        if(!configuration.GetValue("Rag:FeedbackEnabled",false))return;
        var row=new { atUtc=DateTime.UtcNow,questionHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(question.Trim().ToLowerInvariant()))),
            question=configuration.GetValue("Rag:StoreQuestionText",false)?question[..Math.Min(question.Length,1500)]:null,found,helpful };
        await Gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Root);
            var now=DateTime.UtcNow;
            foreach(var file in Directory.EnumerateFiles(Root,"*.jsonl"))if(File.GetLastWriteTimeUtc(file)<now.AddDays(-30))File.Delete(file);
            await File.AppendAllTextAsync(Path.Combine(Root,$"{now:yyyy-MM-dd}.jsonl"),JsonSerializer.Serialize(row)+"\n",ct);
        }
        finally {Gate.Release();}
    }
}
