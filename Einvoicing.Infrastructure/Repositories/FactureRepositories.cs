using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using System.Globalization;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Infrastructure.Repositories;





public sealed class FactureRepository(ContextBaseDeDonnees db) : IFactureRepository
{
    public async Task<Facture?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => await db.Factures.FindAsync([id], ct);

    public async Task<Facture?> ObtenirAvecDetailsAsync(Guid id, CancellationToken ct = default)
        => await db.Factures
            .Include(f => f.Lignes)
            .Include(f => f.Historique)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<(List<Facture> Items, int Total)> ListerAsync(
        Guid entrepriseId, FiltreFacturesRequest filtre, CancellationToken ct = default)
    {
        var query = db.Factures.Where(f => f.EntrepriseId == entrepriseId);

        if (filtre.ClientId.HasValue)
            query = query.Where(f => f.ClientId == filtre.ClientId.Value);

        if (filtre.Statut.HasValue)
            query = query.Where(f => f.Statut == filtre.Statut.Value);

        
        if (filtre.TypeFacture.HasValue)
            query = query.Where(f => f.TypeFacture == filtre.TypeFacture.Value);

        if (filtre.DateDebut.HasValue)
            query = query.Where(f => f.DateEmission >= filtre.DateDebut.Value);

        if (filtre.DateFin.HasValue)
            query = query.Where(f => f.DateEmission <= filtre.DateFin.Value);

        if (filtre.MontantMin.HasValue)
            query = query.Where(f => f.TotalTtc >= filtre.MontantMin.Value);

        if (filtre.MontantMax.HasValue)
            query = query.Where(f => f.TotalTtc <= filtre.MontantMax.Value);

        if (!string.IsNullOrWhiteSpace(filtre.Recherche))
        {
            var t = filtre.Recherche.ToLower();
            query = query.Where(f => f.Numero.ToLower().Contains(t)
                || (f.Reference != null && f.Reference.ToLower().Contains(t)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.DateEmission)
            .Skip((filtre.Page - 1) * filtre.ParPage)
            .Take(filtre.ParPage)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<Facture>> ListerEnRetardAsync(Guid entrepriseId, CancellationToken ct = default)
        => await db.Factures
            .Where(f => f.EntrepriseId == entrepriseId
                && f.DateEcheance < DateTime.UtcNow
                && f.Statut != StatutFacture.Payee
                && f.Statut != StatutFacture.Annulee)
            .OrderBy(f => f.DateEcheance)
            .ToListAsync(ct);

    public async Task<StatistiquesFacturesDto> ObtenirStatistiquesAsync(
        Guid entrepriseId, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow;
        var debutMois = new DateTime(maintenant.Year, maintenant.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var toutes = await db.Factures
            .Where(f => f.EntrepriseId == entrepriseId)
            .ToListAsync(ct);

        var mois = toutes.Where(f => f.DateEmission >= debutMois).ToList();

        return new StatistiquesFacturesDto(
            toutes.Count(f => f.Statut == StatutFacture.Brouillon),
            toutes.Count(f => f.Statut == StatutFacture.Validee),
            toutes.Count(f => f.Statut == StatutFacture.Transmise),
            toutes.Count(f => f.Statut == StatutFacture.Acceptee),
            toutes.Count(f => f.Statut == StatutFacture.Rejetee),
            toutes.Count(f => f.Statut == StatutFacture.Payee),
            toutes.Count(f => f.EstEnRetard),
            mois.Sum(f => f.TotalTtc),
            mois.Sum(f => f.MontantPaye),
            toutes.Where(f => f.Statut != StatutFacture.Payee
                && f.Statut != StatutFacture.Annulee)
                  .Sum(f => f.MontantRestant)
        );
    }

    public async Task<List<VolumesMensuelDto>> ObtenirVolumesMensuelsAsync(
        Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default)
    {
        var culture = CultureInfo.GetCultureInfo("fr-TN");

        var data = await db.Factures
            .Where(f => f.EntrepriseId == entrepriseId
                && f.DateEmission >= debut
                && f.DateEmission < fin)
            .GroupBy(f => new { f.DateEmission.Year, f.DateEmission.Month })
            .Select(g => new {
                g.Key.Year,
                g.Key.Month,
                NbFactures = g.Count(),
                MontantHt = g.Sum(x => x.TotalHt),
                MontantTtc = g.Sum(x => x.TotalTtc)
            })
            .ToListAsync(ct);

        return data
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => new VolumesMensuelDto(
                new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy", culture),
                x.NbFactures,
                x.MontantHt,
                x.MontantTtc))
            .ToList();
    }


    public async Task<List<TvaParTauxDto>> ObtenirRepartitionTvaAsync(
        Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default)
    {
        var data = await (from l in db.LignesFacture
                          join f in db.Factures on l.FactureId equals f.Id
                          where f.EntrepriseId == entrepriseId
                              && f.DateEmission >= debut
                              && f.DateEmission < fin
                          group l by l.TauxTva into g
                          select new {
                              Taux = g.Key,
                              BaseHt = g.Sum(x => x.MontantHt),
                              MontantTva = g.Sum(x => x.MontantTva),
                              MontantTtc = g.Sum(x => x.MontantTtc)
                          })
            .ToListAsync(ct);

        return data
            .OrderBy(x => x.Taux)
            .Select(x => new TvaParTauxDto(x.Taux, x.BaseHt, x.MontantTva, x.MontantTtc))
            .ToList();
    }

    public async Task<List<DelaiPaiementClientDto>> ObtenirDelaisPaiementAsync(
        Guid entrepriseId, DateTime debut, DateTime fin, int top, CancellationToken ct = default)
    {
        var factures = await db.Factures
            .Where(f => f.EntrepriseId == entrepriseId
                && f.DatePaiement != null
                && f.DateEmission >= debut
                && f.DateEmission < fin)
            .Select(f => new { f.ClientId, f.DateEmission, f.DatePaiement })
            .ToListAsync(ct);

        if (factures.Count == 0) return new List<DelaiPaiementClientDto>();

        var clients = await db.Clients
            .Where(c => c.EntrepriseId == entrepriseId)
            .Select(c => new { c.Id, c.Nom })
            .ToListAsync(ct);

        var clientMap = clients.ToDictionary(c => c.Id, c => c.Nom);
        var limit = top <= 0 ? 5 : top;

        return factures
            .GroupBy(f => f.ClientId)
            .Select(g => {
                var delais = g
                    .Select(x => (x.DatePaiement!.Value.Date - x.DateEmission.Date).TotalDays)
                    .Where(d => d >= 0)
                    .ToList();

                var moyenne = delais.Count > 0 ? delais.Average() : 0;
                var nom = clientMap.TryGetValue(g.Key, out var n) ? n : "-";

                return new DelaiPaiementClientDto(
                    g.Key,
                    nom,
                    g.Count(),
                    Math.Round(moyenne, 1));
            })
            .OrderByDescending(d => d.DelaiMoyenJours)
            .Take(limit)
            .ToList();
    }

    public async Task<List<RecapMensuelDto>> ObtenirRecapMensuelAsync(
        Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default)
    {
        var culture = CultureInfo.GetCultureInfo("fr-TN");

        var data = await db.Factures
            .Where(f => f.EntrepriseId == entrepriseId
                && f.DateEmission >= debut
                && f.DateEmission < fin)
            .GroupBy(f => new { f.DateEmission.Year, f.DateEmission.Month })
            .Select(g => new {
                g.Key.Year,
                g.Key.Month,
                NbFactures = g.Count(),
                TotalHt = g.Sum(x => x.TotalHt),
                TotalTva = g.Sum(x => x.TotalTva),
                TotalTtc = g.Sum(x => x.TotalTtc),
                MontantPaye = g.Sum(x => x.MontantPaye),
                MontantImpaye = g.Sum(x => x.TotalTtc - x.MontantPaye)
            })
            .ToListAsync(ct);

        return data
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => {
                var label = new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy", culture);
                var taux = x.TotalTtc > 0
                    ? Math.Round((double)(x.MontantPaye / x.TotalTtc * 100), 1)
                    : 0;
                return new RecapMensuelDto(
                    label,
                    x.NbFactures,
                    x.TotalHt,
                    x.TotalTva,
                    x.TotalTtc,
                    x.MontantPaye,
                    x.MontantImpaye,
                    taux);
            })
            .ToList();
    }
    public async Task<bool> RelanceDejaEnvoyeeAsync(Guid factureId, string action, CancellationToken ct = default)
        => await db.HistoriqueFactures.AnyAsync(h => h.FactureId == factureId && h.Action == action, ct);

    public async Task EnregistrerRelanceAsync(
        Guid factureId, Guid effectuePar, string action, string details, CancellationToken ct = default)
    {
        var hist = HistoriqueFacture.Creer(factureId, effectuePar, action, details);
        await db.HistoriqueFactures.AddAsync(hist, ct);
    }
    public async Task AjouterAsync(Facture facture, CancellationToken ct = default)
        => await db.Factures.AddAsync(facture, ct);

    public void MettreAJour(Facture facture)
        => db.Factures.Update(facture);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}





public sealed class CompteurFactureRepository(ContextBaseDeDonnees db) : ICompteurFactureRepository
{
    public async Task<CompteurFacture?> ObtenirAsync(
        Guid entrepriseId, int annee, int mois, CancellationToken ct = default)
        => await db.CompteurFactures
            .FirstOrDefaultAsync(c => c.EntrepriseId == entrepriseId
                && c.Annee == annee && c.Mois == mois, ct);

    public async Task AjouterAsync(CompteurFacture compteur, CancellationToken ct = default)
        => await db.CompteurFactures.AddAsync(compteur, ct);

    public void MettreAJour(CompteurFacture compteur)
        => db.CompteurFactures.Update(compteur);

    public async Task SauvegarderAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
