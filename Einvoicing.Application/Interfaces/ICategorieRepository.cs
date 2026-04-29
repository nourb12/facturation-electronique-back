using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface ICategorieRepository
{
    Task<CategorieProduit?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CategorieProduit>> ListerAsync(Guid entrepriseId, CancellationToken ct = default);
    Task AjouterAsync(CategorieProduit categorie, CancellationToken ct = default);
    void MettreAJour(CategorieProduit categorie);
    Task SauvegarderAsync(CancellationToken ct = default);
}
