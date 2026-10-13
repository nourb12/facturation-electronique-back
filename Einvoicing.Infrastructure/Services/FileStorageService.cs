using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Einvoicing.Domain.Exceptions;
using System.Security.Cryptography;

namespace Einvoicing.Infrastructure.Services;

public sealed class FileStorageService(IWebHostEnvironment env, IConfiguration config) : IFileStorageService
{
    private const long MaxKycFileSize = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedKycExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp"
    };

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new ValidationMetierException("Le fichier transmis est vide.");

        var isKyc = string.Equals(folder, "kyc", StringComparison.OrdinalIgnoreCase);
        if (isKyc)
            await ValidateKycFileAsync(file, ct);

        var root = GetRoot();

        var safeFolder = string.IsNullOrWhiteSpace(folder) ? "misc" : folder.Trim();
        var dir = Path.Combine(root, safeFolder);
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, name);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await file.CopyToAsync(stream, ct);

        var relative = Path.Combine("uploads", safeFolder, name).Replace("\\", "/");
        return relative;
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ValidationMetierException("Document introuvable.");

        var root = GetRoot();
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        if (!normalized.StartsWith($"uploads{Path.DirectorySeparatorChar}kyc{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            throw new ValidationMetierException("Le document demande est invalide.");

        var fileName = Path.GetFileName(normalized);
        var kycRoot = Path.GetFullPath(Path.Combine(root, "kyc"));
        var candidate = Path.GetFullPath(Path.Combine(kycRoot, fileName));
        if (!candidate.StartsWith(kycRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
            throw new FileNotFoundException("Document KYC introuvable.");

        Stream stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    private async Task ValidateKycFileAsync(IFormFile file, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedKycExtensions.Contains(extension))
            throw new ValidationMetierException("Format de document non autorise. Utilisez PDF, JPG, PNG ou WEBP.");
        if (file.Length > MaxKycFileSize)
            throw new ValidationMetierException("Chaque document KYC est limite a 5 Mo.");

        await using var stream = file.OpenReadStream();
        var header = new byte[16];
        var count = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        if (!HasExpectedSignature(extension, header.AsSpan(0, count)))
            throw new ValidationMetierException("Le contenu du fichier ne correspond pas au format annonce.");

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, leaveOpen: true);
            var sample = new char[Math.Min((int)file.Length, 131_072)];
            var read = await reader.ReadAsync(sample.AsMemory(), ct);
            if (new string(sample, 0, read).Contains("/Encrypt", StringComparison.Ordinal))
                throw new ValidationMetierException("Les PDF proteges par mot de passe ne sont pas acceptes.");
        }
    }

    private static bool HasExpectedSignature(string extension, ReadOnlySpan<byte> bytes) => extension.ToLowerInvariant() switch
    {
        ".pdf" => bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8),
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
        ".png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    private string GetRoot()
    {
        var root = config["Uploads:Root"];
        if (string.IsNullOrWhiteSpace(root)) return Path.Combine(env.ContentRootPath, "uploads");
        return Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root);
    }
}
