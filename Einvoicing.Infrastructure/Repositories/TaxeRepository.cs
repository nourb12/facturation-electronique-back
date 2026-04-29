using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class TaxeRepository(ContextBaseDeDonnees db) : ITaxeRepository
{
    public async Task<Taxe?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Taxes.FindAsync([id], ct);

    public async Task<List<Taxe>> ListerAsync(Guid entrepriseId, CancellationToken ct = default)
        => await db.Taxes
            .Where(t => t.EntrepriseId == entrepriseId)
            .OrderBy(t => t.Titre)
            .ToListAsync(ct);

    public async Task AjouterAsync(Taxe taxe, CancellationToken ct = default)
        => await db.Taxes.AddAsync(taxe, ct);

    public void MettreAJour(Taxe taxe)
        => db.Taxes.Update(taxe);

    public void Supprimer(Taxe taxe)
        => db.Taxes.Remove(taxe);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}