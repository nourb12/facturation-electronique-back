using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class FournisseurRepository(ContextBaseDeDonnees db) : IFournisseurRepository
{
    public Task<Fournisseur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => db.Fournisseurs.FindAsync([id], ct).AsTask();

    public Task<Fournisseur?> ObtenirParMatriculeFiscalAsync(Guid entrepriseId, string matriculeFiscal, CancellationToken ct = default)
    {
        var clean = matriculeFiscal.Trim().ToUpperInvariant();
        return db.Fournisseurs
            .FirstOrDefaultAsync(x => x.EntrepriseId == entrepriseId && x.MatriculeFiscal == clean, ct);
    }

    public Task<List<Fournisseur>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default)
    {
        var clean = terme.Trim().ToLowerInvariant();
        return db.Fournisseurs
            .Where(x => x.EntrepriseId == entrepriseId && x.EstActif
                && (x.Nom.ToLower().Contains(clean)
                    || (x.MatriculeFiscal != null && x.MatriculeFiscal.ToLower().Contains(clean))
                    || (x.Adresse != null && x.Adresse.ToLower().Contains(clean))))
            .OrderBy(x => x.Nom)
            .Take(20)
            .ToListAsync(ct);
    }

    public Task AjouterAsync(Fournisseur fournisseur, CancellationToken ct = default)
        => db.Fournisseurs.AddAsync(fournisseur, ct).AsTask();

    public void MettreAJour(Fournisseur fournisseur)
        => db.Fournisseurs.Update(fournisseur);

    public Task SauvegarderAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
