using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class OtpRepository(ContextBaseDeDonnees db) : IOtpRepository
{
    public async Task<OtpCode?> ObtenirDernierValideAsync(
        Guid utilisateurId, OtpType type, CancellationToken ct = default)
        => await db.OtpCodes
            .Where(o => o.UtilisateurId == utilisateurId
                     && o.Type == type
                     && !o.EstUtilise
                     && o.ExpireLe > DateTime.UtcNow)
            .OrderByDescending(o => o.CreeLe)
            .FirstOrDefaultAsync(ct);

    public async Task AjouterAsync(OtpCode otp, CancellationToken ct = default)
        => await db.OtpCodes.AddAsync(otp, ct);

    public void MettreAJour(OtpCode otp)
        => db.OtpCodes.Update(otp);

    public async Task InvaliderTousAsync(
        Guid utilisateurId, OtpType type, CancellationToken ct = default)
    {
        var otps = await db.OtpCodes
            .Where(o => o.UtilisateurId == utilisateurId
                     && o.Type == type
                     && !o.EstUtilise)
            .ToListAsync(ct);
        foreach (var o in otps) o.Utiliser();
    }

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
