using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class SessionRepository(ContextBaseDeDonnees db) : ISessionRepository
{
    public async Task<List<SessionActive>> ListerAsync(
        Guid utilisateurId, CancellationToken ct = default)
        => await db.Sessions
            .Where(s => s.UtilisateurId == utilisateurId)
            .OrderByDescending(s => s.DerniereActivite)
            .ToListAsync(ct);

    public async Task AjouterAsync(SessionActive s, CancellationToken ct = default)
        => await db.Sessions.AddAsync(s, ct);

    public async Task SupprimerAsync(Guid id, CancellationToken ct = default)
    {
        var session = await db.Sessions.FindAsync([id], ct);
        if (session is not null) db.Sessions.Remove(session);
    }

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
