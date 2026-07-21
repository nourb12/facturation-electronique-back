




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using System.Globalization;

namespace Einvoicing.Application.Services;

public sealed class DashboardService(
    IFactureRepository factureRepo,
    IEntrepriseRepository entrepriseRepo
) : IDashboardService
{
    public async Task<DashboardDto> ObtenirDashboardEntrepriseAsync(
        Guid entrepriseId, CancellationToken ct = default)
    {
        var maintenant = DateTime.UtcNow;
        var debutPeriode = new DateTime(maintenant.Year, maintenant.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-5);
        var moisPrecedents = Enumerable.Range(0, 6)
            .Select(i => debutPeriode.AddMonths(i))
            .ToList();

        var stats = await factureRepo.ObtenirStatistiquesAsync(entrepriseId, ct);

        
        var kpisFactures = new KpisFacturesDto(
            stats.TotalBrouillons,
            stats.TotalValidees,
            stats.TotalTransmises,
            stats.TotalAcceptees,
            stats.TotalRejetees,
            stats.TotalEnRetard,
            stats.TotalBrouillons + stats.TotalValidees
                + stats.TotalTransmises + stats.TotalAcceptees
        );

        
        double dso = stats.MontantTotalMois > 0
            ? (double)(stats.MontantEnAttente / stats.MontantTotalMois) * 30
            : 0;

        decimal tauxEncaissement = stats.MontantTotalMois > 0
            ? Math.Round(stats.MontantEncaisseMois / stats.MontantTotalMois * 100, 1)
            : 0;

        var kpisFinanciers = new KpisFinanciersDto(
            stats.MontantTotalMois,
            stats.MontantEncaisseMois,
            stats.MontantEnAttente,
            0,
            Math.Round(dso, 1),
            tauxEncaissement
        );

        
        var total = stats.TotalValidees + stats.TotalTransmises
                     + stats.TotalAcceptees + stats.TotalRejetees;
        var conformes = stats.TotalTransmises + stats.TotalAcceptees;
        double tauxConformite = total > 0
            ? Math.Round((double)conformes / total * 100, 1)
            : 100;

        var kpisConformite = new KpisConformiteDto(
            stats.TotalValidees,
            conformes,
            stats.TotalRejetees,
            tauxConformite,
            new List<string> { "Matricule fiscal", "TVA multi-taux", "Date échéance" }
        );

        
        var parStatut = new List<FactureParStatutDto>
        {
            new("Brouillon",  stats.TotalBrouillons, 0),
            new("Validée",    stats.TotalValidees,   0),
            new("Transmise",  stats.TotalTransmises, 0),
            new("Acceptée",   stats.TotalAcceptees,  0),
            new("Rejetée",    stats.TotalRejetees,   0),
            new("Payée",      stats.TotalPayees,     stats.MontantEncaisseMois)
        };

        
        var culture = CultureInfo.GetCultureInfo("fr-TN");
        var finPeriode = debutPeriode.AddMonths(moisPrecedents.Count);
        var volumesBruts = await factureRepo.ObtenirVolumesMensuelsAsync(entrepriseId, debutPeriode, finPeriode, ct);
        var volumesMap = (volumesBruts ?? new List<VolumesMensuelDto>())
            .ToDictionary(v => v.Mois, v => v);

        var volumes = moisPrecedents
            .Select(m => {
                var label = m.ToString("MMM yyyy", culture);
                return volumesMap.TryGetValue(label, out var v)
                    ? v
                    : new VolumesMensuelDto(label, 0, 0, 0);
            })
            .ToList();

        
        var filtre = new FiltreFacturesRequest(1, 10);
        var dernieres = await factureRepo.ListerAsync(entrepriseId, filtre, ct);
        var dernieresDto = dernieres.Items
            .Select(f => new FactureListeDto(
                f.Id, f.Numero, "-",
                f.Statut.ToString(), f.TypeFacture.ToString(),
                f.DateEmission, f.DateEcheance,
                f.TotalTtc, f.AppliquerRS, f.MontantRS, f.NetAPayer, f.MontantPaye,
                f.EstEnRetard, f.Devise))
            .ToList();

        
        var alertes = new List<AlerteDto>();
        if (stats.TotalEnRetard > 0)
            alertes.Add(new AlerteDto(
                "RetardPaiement",
                $"{stats.TotalEnRetard} facture(s) en retard de paiement.",
                "Critique", null, DateTime.UtcNow));

        if (stats.TotalRejetees > 0)
            alertes.Add(new AlerteDto(
                "RejetTTN",
                $"{stats.TotalRejetees} facture(s) rejetée(s) par TTN.",
                "Avertissement", null, DateTime.UtcNow));

        if (tauxConformite < 80 && total > 0)
            alertes.Add(new AlerteDto(
                "FaibleConformite",
                $"Taux de conformité faible : {tauxConformite}%",
                "Avertissement", null, DateTime.UtcNow));

        return new DashboardDto(
            kpisFactures, kpisFinanciers, kpisConformite,
            parStatut, volumes, volumes, dernieresDto, alertes);
    }

    public async Task<DashboardAdminDto> ObtenirDashboardAdminAsync(
        CancellationToken ct = default)
    {
        var entreprises = await entrepriseRepo.ListerToutesAsync(ct);
        var actives = entreprises.Count(e => e.EstActive);

        var topEntreprises = new List<EntrepriseActiviteDto>();
        foreach (var e in entreprises.Take(5))
        {
            var stats = await factureRepo.ObtenirStatistiquesAsync(e.Id, ct);
            var total = stats.TotalValidees + stats.TotalTransmises
                          + stats.TotalAcceptees + stats.TotalRejetees;
            var conformes = stats.TotalTransmises + stats.TotalAcceptees;
            var taux = total > 0
                ? Math.Round((double)conformes / total * 100, 1)
                : 100;

            topEntreprises.Add(new EntrepriseActiviteDto(
                e.Id, e.Nom, total, stats.MontantTotalMois, taux));
        }

        var volumeTotal = topEntreprises.Sum(t => t.Volume);

        return new DashboardAdminDto(
            entreprises.Count,
            actives,
            0,
            topEntreprises.Sum(t => t.NbFactures),
            volumeTotal,
            topEntreprises);
    }
}
