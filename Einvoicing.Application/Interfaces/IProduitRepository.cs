using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IProduitRepository
{
    Task<Produit?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<Produit?> ObtenirParCodeAsync(Guid entrepriseId, string code, CancellationToken ct = default);
    Task<(List<Produit> Items, int Total)> ListerAsync(Guid entrepriseId, int page, int parPage, Guid? categorieId, bool? actifSeulement, CancellationToken ct = default);
    Task<List<Produit>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default);
    Task AjouterAsync(Produit produit, CancellationToken ct = default);
    void MettreAJour(Produit produit);
    Task SauvegarderAsync(CancellationToken ct = default);
}
