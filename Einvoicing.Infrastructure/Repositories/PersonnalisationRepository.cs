using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class PersonnalisationRepository(ContextBaseDeDonnees db) : IPersonnalisationRepository
{
    public async Task<Personnalisation?> ObtenirAsync(Guid entrepriseId, CancellationToken ct = default)
        => await db.Personnalisations.FirstOrDefaultAsync(p => p.EntrepriseId == entrepriseId, ct);

    public async Task AjouterAsync(Personnalisation personnalisation, CancellationToken ct = default)
        => await db.Personnalisations.AddAsync(personnalisation, ct);

    public void MettreAJour(Personnalisation personnalisation)
        => db.Personnalisations.Update(personnalisation);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}