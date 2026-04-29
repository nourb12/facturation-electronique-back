using Microsoft.AspNetCore.Http;

namespace Einvoicing.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);
}
