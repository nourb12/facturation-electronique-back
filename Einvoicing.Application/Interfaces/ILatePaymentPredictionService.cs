using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface ILatePaymentPredictionService
{
    Task<PredictionRetardDto> PredireAsync(
        Guid factureId,
        Guid entrepriseId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PredictionRetardFactureDto>> ListerFacturesOuvertesAsync(
        Guid entrepriseId,
        CancellationToken ct = default);
}
