using Einvoicing.Application.Services;
using Microsoft.AspNetCore.Http;

namespace Einvoicing.Application.Interfaces;

public interface IOcrClient
{
    Task<OcrExtractionResult?> ExtractAsync(IEnumerable<IFormFile?> files, CancellationToken ct = default);
}
