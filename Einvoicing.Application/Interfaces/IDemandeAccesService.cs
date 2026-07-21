using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IDemandeAccesService
{
    Task<DemandeAccesConfirmationDto> SoumettreDemandeAsync(
        SoumettreDemandeAccesRequest req, CancellationToken ct = default);
    Task<StatutCompteDto> VerifierStatutAsync(
        Guid utilisateurId, CancellationToken ct = default);
    Task<IReadOnlyList<DemandeAccesDto>> ListerDemandesAsync(
        string? statut, CancellationToken ct = default);
    Task ValiderDemandeAsync(Guid entrepriseId, CancellationToken ct = default);
    Task RejeterDemandeAsync(Guid entrepriseId, string motif, CancellationToken ct = default);
    Task DemanderCorrectionsAsync(
        Guid entrepriseId,
        IReadOnlyCollection<string> flagCodes,
        string? messageAdmin,
        CancellationToken ct = default);
}
