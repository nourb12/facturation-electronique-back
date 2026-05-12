using Einvoicing.Application.Services;
using Einvoicing.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Einvoicing.Application.Interfaces;

public interface IOcrClient
{
    Task<OcrExtractionResult?> ExtractAsync(IEnumerable<IFormFile?> files, CancellationToken ct = default);
    Task<AccountingOcrResult?> ExtractAccountingAsync(IFormFile file, string documentType, CancellationToken ct = default);
}
