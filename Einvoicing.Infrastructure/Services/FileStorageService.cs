using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Einvoicing.Infrastructure.Services;

public sealed class FileStorageService(IWebHostEnvironment env, IConfiguration config) : IFileStorageService
{
    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        var root = config["Uploads:Root"];
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(env.ContentRootPath, "uploads");
        }
        else if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(env.ContentRootPath, root);
        }

        var safeFolder = string.IsNullOrWhiteSpace(folder) ? "misc" : folder.Trim();
        var dir = Path.Combine(root, safeFolder);
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(file.FileName);
        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, name);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await file.CopyToAsync(stream, ct);

        var relative = Path.Combine("uploads", safeFolder, name).Replace("\\", "/");
        return relative;
    }
}
