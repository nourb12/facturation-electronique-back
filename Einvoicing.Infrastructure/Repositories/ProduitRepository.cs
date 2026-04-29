using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class ProduitRepository(ContextBaseDeDonnees db) : IProduitRepository
{
    public async Task<Produit?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Produits.FindAsync([id], ct);

    public async Task<Produit?> ObtenirParCodeAsync(Guid entrepriseId, string code, CancellationToken ct = default)
        => await db.Produits
            .FirstOrDefaultAsync(p => p.EntrepriseId == entrepriseId
                && p.Code == code.ToUpperInvariant(), ct);

    public async Task<(List<Produit> Items, int Total)> ListerAsync(
        Guid entrepriseId, int page, int parPage,
        Guid? categorieId, bool? actifSeulement, CancellationToken ct = default)
    {
        var query = db.Produits.Where(p => p.EntrepriseId == entrepriseId);

        if (categorieId.HasValue)
            query = query.Where(p => p.CategorieId == categorieId.Value);

        if (actifSeulement.HasValue)
            query = query.Where(p => p.EstActif == actifSeulement.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Libelle)
            .Skip((page - 1) * parPage)
            .Take(parPage)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<Produit>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default)
    {
        var t = terme.ToLowerInvariant();
        return await db.Produits
            .Where(p => p.EntrepriseId == entrepriseId && p.EstActif
                && (p.Libelle.ToLower().Contains(t) || p.Code.ToLower().Contains(t)))
            .OrderBy(p => p.Libelle)
            .Take(20)
            .ToListAsync(ct);
    }

    public async Task AjouterAsync(Produit produit, CancellationToken ct = default)
        => await db.Produits.AddAsync(produit, ct);

    public void MettreAJour(Produit produit)
        => db.Produits.Update(produit);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
