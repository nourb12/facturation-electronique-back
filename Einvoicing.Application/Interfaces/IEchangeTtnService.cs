using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IEchangeTtnService
{
    Task<EchangeDto> EnvoyerAsync(Guid factureId, Guid entrepriseId, Guid envoyePar, CancellationToken ct = default);
    Task<EchangeDto> SimulerReponseAsync(Guid echangeId, string scenarioSimulation, CancellationToken ct = default);
    Task<EchangeDto> ObtenirStatutAsync(Guid factureId, Guid entrepriseId, CancellationToken ct = default);
    Task<EchangeDto> RelancerAsync(Guid echangeId, Guid entrepriseId, CancellationToken ct = default);
}
