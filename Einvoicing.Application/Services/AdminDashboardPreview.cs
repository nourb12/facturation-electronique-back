using System.Globalization;
using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Services;

// Deliberately isolated from repositories: preview requests never mutate business data.
public static class AdminDashboardPreview
{
    public static AdminDashboardPreviewDto Create(string period, DateTimeOffset now)
    {
        if (period is not ("7d" or "30d" or "12m"))
            throw new ArgumentOutOfRangeException(nameof(period));
        int accepted = period == "7d" ? 255 : period == "30d" ? 1210 : 11785;
        int pending = period == "7d" ? 7 : 41;
        int rejected = period == "7d" ? 7 : period == "30d" ? 24 : 196;
        int errors = period == "7d" ? 3 : period == "30d" ? 9 : 67;
        int issued = accepted + pending + rejected + errors;
        decimal volume = period == "7d" ? 87340m : period == "30d" ? 412650m : 3980420m;
        var count = period == "7d" ? 7 : period == "30d" ? 10 : 12;
        var flow = Enumerable.Range(0, count).Select(i => {
            var date = period == "12m" ? now.AddMonths(i - count + 1) : now.AddDays((i - count + 1) * (period == "30d" ? 3 : 1));
            var value = period == "12m" ? 620 + i * 61 : period == "30d" ? 38 + i * 3 : new[] {41, 48, 52, 46, 55, 18, 12}[i];
            return new PreviewFlowDto(date.ToString(period == "12m" ? "MMM" : "dd/MM", CultureInfo.GetCultureInfo("fr-FR")), value, (int)Math.Round(value * .942));
        }).ToArray();
        // Keep the graph and period counters coherent, including integer rounding.
        var rawTotal = flow.Sum(x => x.Issued);
        int remainingIssued = issued, remainingAccepted = accepted;
        flow = flow.Select((point, index) => {
            int e = index == flow.Length - 1 ? remainingIssued : (int)Math.Floor((double)point.Issued / rawTotal * issued);
            int a = (int)Math.Round((issued - remainingIssued + e) * (decimal)accepted / issued) - (accepted - remainingAccepted);
            remainingIssued -= e; remainingAccepted -= a;
            return point with { Issued = e, Accepted = a };
        }).ToArray();
        var rate = Math.Round(100m * accepted / issued, 1);
        return new(true, period, now,
            [
                new("companies", "Entreprises actives", 11, "", "sur 16 entreprises", 2, "", true),
                new("users", "Utilisateurs actifs", 38, "", "comptes sur la période", 5, "", true),
                new("invoices", "Factures émises", issued, "", "sur la période", 7.4m, "%", true),
                new("volume", "Volume facturé", volume, "DT", "sur la période", 9.1m, "%", true),
                new("acceptance", "Acceptation TTN", rate, "%", "acceptées / émises", -.6m, "pt", true),
                new("revenue", "Revenu mensuel", 3586, "DT", "MRR actuel", 4.2m, "%", true)
            ],
            [
                new("access", "Demandes d'accès", 3, "1 dossier en attente depuis plus de 48 h", "warning"),
                new("demos", "Démos à confirmer", 1, "Prochaine session : " + now.AddDays(2).ToString("dd/MM") + " à 15:00", "info"),
                new("rejections", "Factures rejetées TTN", rejected, "Corrections à effectuer par les clients", "danger"),
                new("certificates", "Certificats à renouveler", 2, "Expiration sous 30 jours", "warning"),
                new("anomalies", "Anomalies détectées", 4, "Transactions à examiner", "info")
            ], flow,
            [new("accepted", "Acceptées", accepted, "#16a34a"), new("pending", "En transmission", pending, "#3b82f6"), new("rejected", "Rejetées", rejected, "#dc2626"), new("errors", "En erreur", errors, "#d97706")],
            [new("API plateforme", "Disponible", "42 ms", "success"), new("Base de données", "Disponible", "8 ms", "success"), new("Portail TTN", "Disponible", "380 ms", "success"), new("Analyse IA", "Disponible", "120 ms", "success"), new("E-mail SMTP", "Disponible", "Simulation", "success"), new("SMS", "Non configuré", "Aucun envoi réel", "warning")],
            [new("demo-alpha", "Alpha Systems", "1495908/S", 98, 0, "Pro"), new("demo-beta", "Beta Industrie", "1280345/A", 71, 2, "Growth"), new("demo-gamma", "Gamma Corp", "1672459/B", 40, 9, "Starter"), new("demo-delta", "Delta Services", "1789362/C", 55, 5, "Growth"), new("demo-tech", "Tech Solutions", "1823456/D", 0, null, "Starter")],
            [new("Starter", 6, "#94a3b8"), new("Growth", 5, "#3b82f6"), new("Pro", 3, "#16a34a"), new("Enterprise", 2, "#8b5cf6")],
            new(3, 0, 100, 4),
            [
                new("act-1", now.AddMinutes(-12), "Amina Trabelsi", "a validé une demande d'accès", "Tech Solutions", "192.0.2.10", "admin", "success", "Succès"),
                new("act-2", now.AddMinutes(-18), "TTN", "a accepté la facture FAC-0128", "Alpha Systems · 13 056 DT", "-", "system", "success", "Acceptée"),
                new("act-3", now.AddHours(-1), "Nour Ben Mna", "a confirmé une démo", "Beta Industrie", "192.0.2.20", "admin", "info", "Confirmée"),
                new("act-4", now.AddHours(-2), "Compte administrateur", "3 tentatives de connexion ont échoué", "Authentification", "198.51.100.12", "login", "warning", "Alerte"),
                new("act-5", now.AddHours(-4), "Amina Trabelsi", "a attribué le rôle Comptable à Sami Ben Ali", "Rôles et permissions", "192.0.2.10", "admin", "success", "Succès"),
                new("act-6", now.AddHours(-8), "Système", "a terminé la sauvegarde quotidienne", "Base de données · 2,4 Go", "-", "system", "success", "Succès"),
                new("act-7", now.AddHours(-12), "TTN", "a rejeté la facture FAC-0119", "Beta Industrie · matricule invalide", "-", "system", "danger", "Rejetée"),
                new("act-8", now.AddHours(-16), "Amina Trabelsi", "s'est connectée avec vérification 2FA", "Authentification", "192.0.2.10", "login", "success", "Succès")
            ],
            BuildReasons(rejected),
            [new("Alpha Systems", now.AddDays(12), 12), new("Beta Industrie", now.AddDays(27), 27), new("Gamma Corp", now.AddDays(94), 94), new("Delta Services", now.AddDays(210), 210)],
            [new("FAC-0129", "Alpha Systems", 4250m, "En transmission", "Traitement TTN"), new("FAC-0119", "Beta Industrie", 18760m, "Rejetée", "Matricule fiscal invalide"), new("FAC-0118", "Gamma Corp", 9600m, "Rejetée", "Total TVA incohérent"), new("FAC-0116", "Delta Services", 3100m, "En erreur", "Délai de connexion dépassé")],
            [new("KYC-001", "Tech Solutions", "Sami Ben Ali", "access", "À examiner", now.AddHours(-54)), new("KYC-002", "Atlas Conseil", "Rim Mansour", "access", "À examiner", now.AddHours(-8)), new("KYC-003", "Carthage Digital", "Yassine Ali", "access", "À examiner", now.AddHours(-3)), new("DEMO-001", "Beta Industrie", "Leila Messaoud", "demos", "À confirmer", now.AddDays(2))],
            [new("TX-108", "Alpha Systems", "Montant inhabituel : 42 000 DT", "warning"), new("TX-109", "Beta Industrie", "Deux transactions similaires à 4 minutes d'intervalle", "danger"), new("TX-110", "Gamma Corp", "Coordonnées bancaires modifiées", "warning"), new("TX-111", "Delta Services", "Paiement hors plage habituelle", "info")],
            3586m, 3, 5, 1.4m);
    }

    private static PreviewReasonDto[] BuildReasons(int total)
    {
        string[] labels = ["Matricule fiscal invalide", "Total TVA incohérent", "Signature absente ou expirée", "Format TEIF non conforme", "Autre"];
        int[] weights = [9, 6, 5, 3, 1];
        var rows = weights.Select((weight, i) => new PreviewReasonDto(labels[i], (int)Math.Floor(total * weight / 24m))).ToArray();
        rows[0] = rows[0] with { Count = rows[0].Count + total - rows.Sum(x => x.Count) };
        return rows;
    }
}
