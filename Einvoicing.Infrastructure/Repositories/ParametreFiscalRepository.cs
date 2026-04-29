using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class ParametreFiscalRepository(ContextBaseDeDonnees db) : IParametreFiscalRepository
{
    public async Task<ParametreFiscal?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.ParametresFiscaux.FindAsync([id], ct);

    public async Task<List<ParametreFiscal>> ListerAsync(Guid entrepriseId, CancellationToken ct = default)
        => await db.ParametresFiscaux
            .Where(p => p.EntrepriseId == entrepriseId)
            .OrderBy(p => p.Libelle)
            .ToListAsync(ct);

    public async Task AjouterAsync(ParametreFiscal parametre, CancellationToken ct = default)
        => await db.ParametresFiscaux.AddAsync(parametre, ct);

    public void MettreAJour(ParametreFiscal parametre)
        => db.ParametresFiscaux.Update(parametre);

    public void Supprimer(ParametreFiscal parametre)
        => db.ParametresFiscaux.Remove(parametre);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}