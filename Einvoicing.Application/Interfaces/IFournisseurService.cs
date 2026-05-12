using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IFournisseurService
{
    Task<FournisseurMatchResultDto> MatchOrCreateAsync(Guid entrepriseId, MatchOrCreateFournisseurRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<FournisseurDto>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default);
    Task<FournisseurDto?> ObtenirParIdAsync(Guid entrepriseId, Guid id, CancellationToken ct = default);
}
