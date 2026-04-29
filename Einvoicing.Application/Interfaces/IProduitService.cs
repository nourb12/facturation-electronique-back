using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.Interfaces;

public interface IProduitService
{
    Task<ProduitDto> CreerAsync(Guid entrepriseId, CreerProduitRequest request, CancellationToken ct = default);
    Task<ProduitDto> ObtenirParIdAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task<ListeProduitsDto> ListerAsync(Guid entrepriseId, int page, int parPage, Guid? categorieId, bool? actifSeulement, CancellationToken ct = default);
    Task<ProduitDto> MettreAJourAsync(Guid id, Guid entrepriseId, MettreAJourProduitRequest request, CancellationToken ct = default);
    Task DesactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task ReactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task<IReadOnlyList<ProduitDto>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default);
}
