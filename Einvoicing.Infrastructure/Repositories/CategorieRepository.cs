using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class CategorieRepository(ContextBaseDeDonnees db) : ICategorieRepository
{
    public async Task<CategorieProduit?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Categories
            .Include(c => c.Produits)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<CategorieProduit>> ListerAsync(Guid entrepriseId, CancellationToken ct = default)
        => await db.Categories
            .Include(c => c.Produits)
            .Where(c => c.EntrepriseId == entrepriseId && c.EstActive)
            .OrderBy(c => c.Nom)
            .ToListAsync(ct);

    public async Task AjouterAsync(CategorieProduit categorie, CancellationToken ct = default)
        => await db.Categories.AddAsync(categorie, ct);

    public void MettreAJour(CategorieProduit categorie)
        => db.Categories.Update(categorie);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
