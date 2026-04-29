using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class ClientRepository(ContextBaseDeDonnees db) : IClientRepository
{
    public async Task<Client?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Clients.FindAsync([id], ct);

    public async Task<Client?> ObtenirParEmailAsync(Guid entrepriseId, string email, CancellationToken ct = default)
        => await db.Clients
            .FirstOrDefaultAsync(c => c.EntrepriseId == entrepriseId
                && c.Email == email.ToLowerInvariant(), ct);

    public async Task<(List<Client> Items, int Total)> ListerAsync(
        Guid entrepriseId, int page, int parPage, bool? actifSeulement, CancellationToken ct = default)
    {
        var query = db.Clients.Where(c => c.EntrepriseId == entrepriseId);

        if (actifSeulement.HasValue)
            query = query.Where(c => c.EstActif == actifSeulement.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Nom)
            .Skip((page - 1) * parPage)
            .Take(parPage)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<Client>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default)
    {
        var t = terme.ToLowerInvariant();
        return await db.Clients
            .Where(c => c.EntrepriseId == entrepriseId && c.EstActif
                && (c.Nom.ToLower().Contains(t)
                    || c.Email.ToLower().Contains(t)
                    || (c.MatriculeFiscal != null && c.MatriculeFiscal.ToLower().Contains(t))))
            .OrderBy(c => c.Nom)
            .Take(20)
            .ToListAsync(ct);
    }

    public async Task AjouterAsync(Client client, CancellationToken ct = default)
        => await db.Clients.AddAsync(client, ct);

    public void MettreAJour(Client client)
        => db.Clients.Update(client);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
