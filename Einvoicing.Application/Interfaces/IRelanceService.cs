namespace Einvoicing.Application.Interfaces;

public interface IRelanceService
{
    Task ExecuterRelancesAsync(CancellationToken ct = default);
    Task EnvoyerRelancePredictiveAsync(
        Guid factureId,
        Guid entrepriseId,
        Guid utilisateurId,
        CancellationToken ct = default);
}
