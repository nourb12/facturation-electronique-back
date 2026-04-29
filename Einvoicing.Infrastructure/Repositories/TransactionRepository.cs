using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class TransactionRepository(ContextBaseDeDonnees db) : ITransactionRepository
{
    public async Task<Transaction?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Transactions.FindAsync([id], ct);

    public async Task<(List<Transaction> Items, int Total)> ListerAsync(
        Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
    {
        var query = ApplyFilters(db.Transactions.AsNoTracking(), entrepriseId, filtre);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.Date)
            .Skip((filtre.Page - 1) * filtre.ParPage)
            .Take(filtre.ParPage)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<Transaction>> ListerTousAsync(
        Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
    {
        var query = ApplyFilters(db.Transactions.AsNoTracking(), entrepriseId, filtre);
        return await query
            .OrderByDescending(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task<TransactionCountersDto> CountersAsync(
        Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
    {
        var query = ApplyFilters(db.Transactions.AsNoTracking(), entrepriseId, filtre);

        var groups = await query
            .GroupBy(t => t.Statut)
            .Select(g => new
            {
                Statut = g.Key,
                Count = g.Count(),
                Montant = g.Sum(x => x.Type == TypeTransaction.Sortie ? -x.Montant : x.Montant)
            })
            .ToListAsync(ct);

        int cNon = 0, cAtt = 0, cJus = 0;
        decimal mNon = 0, mAtt = 0, mJus = 0;

        foreach (var g in groups)
        {
            if (g.Statut == StatutTransaction.NonJustifiee) { cNon = g.Count; mNon = g.Montant; }
            else if (g.Statut == StatutTransaction.EnAttente) { cAtt = g.Count; mAtt = g.Montant; }
            else if (g.Statut == StatutTransaction.Justifiee) { cJus = g.Count; mJus = g.Montant; }
        }

        return new TransactionCountersDto(cNon, mNon, cAtt, mAtt, cJus, mJus);
    }

    public async Task<List<TransactionCategorieResumeDto>> ResumeParCategorieAsync(
        Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default)
    {
        var query = ApplyFilters(db.Transactions.AsNoTracking(), entrepriseId, filtre);

        var rows = await query
            .GroupBy(t => new { Cat = t.CategorieNom ?? "Non catégorisé", t.Type })
            .Select(g => new TransactionCategorieResumeDto(
                g.Key.Cat,
                g.Key.Cat,
                g.Key.Type,
                g.Count(),
                g.Sum(x => x.Montant)
            ))
            .OrderByDescending(x => x.Montant)
            .ToListAsync(ct);

        return rows;
    }

    public async Task AjouterAsync(Transaction transaction, CancellationToken ct = default)
        => await db.Transactions.AddAsync(transaction, ct);

    public void MettreAJour(Transaction transaction)
        => db.Transactions.Update(transaction);

    public void Supprimer(Transaction transaction)
        => db.Transactions.Remove(transaction);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);

    private static IQueryable<Transaction> ApplyFilters(
        IQueryable<Transaction> query,
        Guid entrepriseId,
        FiltreTransactionsRequest filtre)
    {
        query = query.Where(t => t.EntrepriseId == entrepriseId);

        if (filtre.DateDebut.HasValue)
            query = query.Where(t => t.Date >= filtre.DateDebut.Value);
        if (filtre.DateFin.HasValue)
            query = query.Where(t => t.Date <= filtre.DateFin.Value);

        if (filtre.Statut.HasValue)
            query = query.Where(t => t.Statut == filtre.Statut.Value);

        if (filtre.Type.HasValue)
            query = query.Where(t => t.Type == filtre.Type.Value);

        if (!string.IsNullOrWhiteSpace(filtre.CategorieNom))
        {
            var cat = filtre.CategorieNom.Trim().ToLowerInvariant();
            query = query.Where(t => (t.CategorieNom ?? string.Empty).ToLower().Contains(cat));
        }

        if (!string.IsNullOrWhiteSpace(filtre.Recherche))
        {
            var r = filtre.Recherche.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Libelle.ToLower().Contains(r)
                || (t.TiersNom != null && t.TiersNom.ToLower().Contains(r))
                || (t.CategorieNom != null && t.CategorieNom.ToLower().Contains(r)));
        }

        return query;
    }
}

