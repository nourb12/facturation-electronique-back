using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;





public sealed class PaiementRepository(ContextBaseDeDonnees db) : IPaiementRepository
{
    public async Task<Paiement?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Paiements.FindAsync([id], ct);

    public async Task<List<Paiement>> ListerParFactureAsync(
        Guid factureId, CancellationToken ct = default)
        => await db.Paiements
            .Where(p => p.FactureId == factureId)
            .OrderByDescending(p => p.DatePaiement)
            .ToListAsync(ct);

    public async Task<(List<Paiement> Items, int Total)> ListerParEntrepriseAsync(
        Guid entrepriseId, int page, int parPage, CancellationToken ct = default)
    {
        var query = db.Paiements.Where(p => p.EntrepriseId == entrepriseId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.DatePaiement)
            .Skip((page - 1) * parPage)
            .Take(parPage)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task AjouterAsync(Paiement paiement, CancellationToken ct = default)
        => await db.Paiements.AddAsync(paiement, ct);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}





public sealed class SignatureRepository(ContextBaseDeDonnees db) : ISignatureRepository
{
    public async Task<SignatureRequest?> ObtenirParIdAsync(
        Guid id, CancellationToken ct = default)
        => await db.Signatures.FindAsync([id], ct);

    public async Task<SignatureRequest?> ObtenirParFactureAsync(
        Guid factureId, CancellationToken ct = default)
        => await db.Signatures
            .Where(s => s.FactureId == factureId)
            .OrderByDescending(s => s.CreeLe)
            .FirstOrDefaultAsync(ct);

    public async Task AjouterAsync(SignatureRequest sig, CancellationToken ct = default)
        => await db.Signatures.AddAsync(sig, ct);

    public void MettreAJour(SignatureRequest sig)
        => db.Signatures.Update(sig);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}





public sealed class EchangeRepository(ContextBaseDeDonnees db) : IEchangeRepository
{
    public async Task<ExternalExchange?> ObtenirParIdAsync(
        Guid id, CancellationToken ct = default)
        => await db.Echanges.FindAsync([id], ct);

    public async Task<ExternalExchange?> ObtenirDernierParFactureAsync(
        Guid factureId, CancellationToken ct = default)
        => await db.Echanges
            .Where(e => e.FactureId == factureId)
            .OrderByDescending(e => e.CreeLe)
            .FirstOrDefaultAsync(ct);

    public async Task<List<ExternalExchange>> ListerParFactureAsync(
        Guid factureId, CancellationToken ct = default)
        => await db.Echanges
            .Where(e => e.FactureId == factureId)
            .OrderByDescending(e => e.CreeLe)
            .ToListAsync(ct);

    public async Task AjouterAsync(ExternalExchange echange, CancellationToken ct = default)
        => await db.Echanges.AddAsync(echange, ct);

    public void MettreAJour(ExternalExchange echange)
        => db.Echanges.Update(echange);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
