using Einvoicing.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Einvoicing.Application.Interfaces;

public interface IScanService
{
    Task<ScanUploadResponseDto> UploadAsync(
        Guid entrepriseId,
        Guid utilisateurId,
        IFormFile file,
        string documentType,
        CancellationToken ct = default);

    Task<ValidateScanResponseDto> ValidateAsync(
        Guid entrepriseId,
        Guid utilisateurId,
        ValidateScanRequest request,
        CancellationToken ct = default);
}
