using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IPaiementService
{
    Task<PaiementDto> EnregistrerAsync(Guid entrepriseId, Guid enregistrePar, EnregistrerPaiementRequest request, CancellationToken ct = default);
    Task<ListePaiementsDto> ListerParFactureAsync(Guid factureId, Guid entrepriseId, CancellationToken ct = default);
    Task<ListePaiementsDto> ListerParEntrepriseAsync(Guid entrepriseId, int page, int parPage, CancellationToken ct = default);
}
