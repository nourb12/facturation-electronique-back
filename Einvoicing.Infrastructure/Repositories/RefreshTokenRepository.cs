using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class RefreshTokenRepository(ContextBaseDeDonnees db) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> ObtenirParTokenAsync(string token, CancellationToken ct = default)
        => await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);

    public async Task AjouterAsync(RefreshToken rt, CancellationToken ct = default)
        => await db.RefreshTokens.AddAsync(rt, ct);

    public void MettreAJour(RefreshToken rt)
        => db.RefreshTokens.Update(rt);

    public async Task RevoquerTousAsync(Guid utilisateurId, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens
            .Where(r => r.UtilisateurId == utilisateurId && !r.EstRevoque)
            .ToListAsync(ct);
        foreach (var t in tokens) t.Revoquer();
    }

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
