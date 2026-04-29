using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class UtilisateurRepository(ContextBaseDeDonnees db) : IUtilisateurRepository
{
    public async Task<Utilisateur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Utilisateurs.FindAsync([id], ct);

    public async Task<Utilisateur?> ObtenirParEmailAsync(string email, CancellationToken ct = default)
        => await db.Utilisateurs
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), ct);

    public async Task AjouterAsync(Utilisateur u, CancellationToken ct = default)
        => await db.Utilisateurs.AddAsync(u, ct);

    public void MettreAJour(Utilisateur u)
        => db.Utilisateurs.Update(u);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    public async Task<List<Utilisateur>> ListerParEntrepriseAsync(
        Guid entrepriseId, CancellationToken ct = default)
        => await db.Utilisateurs
            .Where(u => u.EntrepriseId == entrepriseId)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(ct);
    public async Task<List<Utilisateur>> ListerParStatutAsync(
        StatutCompte statut, CancellationToken ct = default)
        => await db.Utilisateurs
            .Where(u => u.Statut == statut)
            .OrderBy(u => u.CreeLe)
            .ToListAsync(ct);
}
