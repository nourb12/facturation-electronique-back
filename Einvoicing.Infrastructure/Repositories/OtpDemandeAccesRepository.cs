using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class OtpDemandeAccesRepository(ContextBaseDeDonnees db) : IOtpDemandeAccesRepository
{
    public Task<OtpDemandeAcces?> ObtenirAsync(Guid id, CancellationToken ct = default)
        => db.OtpDemandesAcces.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task AjouterAsync(OtpDemandeAcces otp, CancellationToken ct = default)
        => db.OtpDemandesAcces.AddAsync(otp, ct).AsTask();

    public void MettreAJour(OtpDemandeAcces otp) => db.MarquerModifie(otp);
    public Task SauvegarderAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
