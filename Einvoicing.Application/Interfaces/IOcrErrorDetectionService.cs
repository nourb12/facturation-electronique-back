using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IOcrErrorDetectionService
{
    Task<OcrErrorScoreDto> ScoreAsync(OcrErrorScoreRequest request, CancellationToken ct = default);
}
