using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class EntrepriseRepository(ContextBaseDeDonnees db) : IEntrepriseRepository
{
    public async Task<Entreprise?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Entreprises.FindAsync([id], ct);

    public async Task<Entreprise?> ObtenirParMatriculeAsync(string matricule, CancellationToken ct = default)
        => await db.Entreprises
            .FirstOrDefaultAsync(e => e.MatriculeFiscal == matricule.ToUpperInvariant(), ct);

    public async Task<List<Entreprise>> ListerToutesAsync(CancellationToken ct = default)
        => await db.Entreprises
            .OrderBy(e => e.Nom)
            .ToListAsync(ct);

    public async Task AjouterAsync(Entreprise entreprise, CancellationToken ct = default)
        => await db.Entreprises.AddAsync(entreprise, ct);

    public void MettreAJour(Entreprise entreprise)
        => db.Entreprises.Update(entreprise);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
