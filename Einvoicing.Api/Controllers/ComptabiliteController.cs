using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/comptabilite")]
[Produces("application/json")]
[Authorize]
public sealed class ComptabiliteController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();

        var factures = await db.Factures.Where(f => f.EntrepriseId == entrepriseId).ToListAsync(ct);
        var paiements = await db.Paiements.Where(p => p.EntrepriseId == entrepriseId).ToListAsync(ct);
        var transactions = await db.Transactions.Where(t => t.EntrepriseId == entrepriseId).ToListAsync(ct);
        var clients = await db.Clients.Where(c => c.EntrepriseId == entrepriseId).ToDictionaryAsync(c => c.Id, c => c.Nom, ct);
        var taxes = await db.Taxes.CountAsync(t => t.EntrepriseId == entrepriseId, ct);
        var rs = await db.ParametresFiscaux.CountAsync(p => p.EntrepriseId == entrepriseId && p.InclureRetenueSource && p.EstActif, ct);

        var plan = PlanComptable();
        var ecritures = BuildEcritures(factures, paiements, transactions, clients);
        var balance = BuildBalance(ecritures, plan);
        var tvaDeductible = transactions.Where(t => t.RecoverableVatAmount.HasValue).Sum(t => t.RecoverableVatAmount!.Value);
        var tvaCollectee = factures.Where(f => f.TypeFacture != TypeFacture.Avoir).Sum(f => f.TotalTva);
        var produits = factures.Where(f => f.TypeFacture != TypeFacture.Avoir).Sum(f => f.TotalHt)
            + transactions.Where(t => t.Type == TypeTransaction.Entree).Sum(t => t.Montant);
        var charges = transactions.Where(t => t.Type == TypeTransaction.Sortie).Sum(t => t.Montant);

        return Ok(new ComptabiliteDashboardDto(
            Kpis:
            [
                new("resultat", "Resultat estime", Math.Round(produits - charges, 3), "money", "Produits classe 7 moins charges classe 6", "ti-chart-bar", "yellow"),
                new("tva", "TVA nette due", Math.Round(tvaCollectee - tvaDeductible, 3), "money", "Collectee moins deductible", "ti-receipt-tax", "blue"),
                new("rs", "RS actives", rs, "number", "Retenues source modele 41", "ti-percentage", "orange"),
                new("ecritures", "Ecritures", ecritures.Count, "number", "Factures, paiements, OCR mobile et OD", "ti-file-pencil", "green")
            ],
            Alertes:
            [
                new("D15", "warning", "ti-alert-triangle", "Declaration mensuelle D15", $"{taxes} taxes configurees: TVA, FODEC, TCL et timbre fiscal.", "28/mois"),
                new("RS41", "danger", "ti-shield-exclamation", "Retenues a la source", $"{rs} taux actifs pour le recapitulatif modele 41.", "28/mois"),
                new("OCR", "info", "ti-file-check", "OCR mobile connecte", $"{transactions.Count(t => t.Source == DocumentSource.MobileApp)} transactions mobiles a tester.", "Temps reel")
            ],
            CalendrierFiscal: CalendrierFiscal(),
            PlanComptable: plan,
            Ecritures: ecritures,
            Journaux: BuildJournaux(ecritures),
            Balance: balance,
            GrandLivre: BuildGrandLivre(balance, ecritures),
            Actifs: Actifs(),
            Etats: Etats(),
            Exercices: Exercices(),
            Analytique: BuildAnalytique(transactions)));
    }

    [HttpGet("plan-comptable")] public IActionResult Plan() => Ok(PlanComptable());
    [HttpGet("calendrier-fiscal")] public IActionResult Fiscal() => Ok(CalendrierFiscal());
    [HttpGet("actifs")] public IActionResult Immobilisations() => Ok(Actifs());
    [HttpGet("etats")] public IActionResult EtatsComptables() => Ok(Etats());
    [HttpGet("exercices")] public IActionResult ExercicesComptables() => Ok(Exercices());

    [HttpGet("ecritures")]
    public async Task<IActionResult> Ecritures(CancellationToken ct) => Ok((await LoadAsync(ct)).Ecritures);

    [HttpGet("journaux")]
    public async Task<IActionResult> Journaux(CancellationToken ct) => Ok(BuildJournaux((await LoadAsync(ct)).Ecritures));

    [HttpGet("balance")]
    public async Task<IActionResult> Balance(CancellationToken ct) => Ok(BuildBalance((await LoadAsync(ct)).Ecritures, PlanComptable()));

    [HttpGet("grand-livre")]
    public async Task<IActionResult> GrandLivre(CancellationToken ct)
    {
        var data = await LoadAsync(ct);
        var balance = BuildBalance(data.Ecritures, PlanComptable());
        return Ok(BuildGrandLivre(balance, data.Ecritures));
    }

    [HttpGet("analytique")]
    public async Task<IActionResult> Analytique(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return Ok(Array.Empty<AnalytiqueDto>());
        var transactions = await db.Transactions.Where(t => t.EntrepriseId == entrepriseId).ToListAsync(ct);
        return Ok(BuildAnalytique(transactions));
    }

    private async Task<ComptabiliteData> LoadAsync(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return new([]);

        var factures = await db.Factures.Where(f => f.EntrepriseId == entrepriseId).ToListAsync(ct);
        var paiements = await db.Paiements.Where(p => p.EntrepriseId == entrepriseId).ToListAsync(ct);
        var transactions = await db.Transactions.Where(t => t.EntrepriseId == entrepriseId).ToListAsync(ct);
        var clients = await db.Clients.Where(c => c.EntrepriseId == entrepriseId).ToDictionaryAsync(c => c.Id, c => c.Nom, ct);
        return new(BuildEcritures(factures, paiements, transactions, clients));
    }

    private async Task<Guid?> EntrepriseIdAsync(CancellationToken ct)
        => currentUser.EntrepriseId ?? await db.Entreprises.OrderByDescending(e => e.ModifieLe).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);

    private static List<EcritureDto> BuildEcritures(
        List<Facture> factures,
        List<Paiement> paiements,
        List<Transaction> transactions,
        Dictionary<Guid, string> clients)
    {
        var rows = new List<EcritureDto>();
        var i = 1;

        foreach (var facture in factures.OrderByDescending(f => f.DateEmission).Take(40))
        {
            var client = clients.GetValueOrDefault(facture.ClientId, "Client");
            var journal = facture.TypeFacture == TypeFacture.Avoir ? "AV" : "VT";
            var signe = facture.TypeFacture == TypeFacture.Avoir ? -1 : 1;

            rows.Add(Row(i++, facture.DateEmission, journal, facture.Numero, "411000",
                facture.TypeFacture == TypeFacture.Avoir ? "Avoir client" : "Creance client",
                client,
                Math.Max(0, signe * facture.TotalTtc),
                Math.Max(0, -signe * facture.TotalTtc),
                facture.Statut.ToString()));

            rows.Add(Row(i++, facture.DateEmission, journal, facture.Numero, "707000",
                "Produits factures",
                client,
                Math.Max(0, -signe * facture.TotalHt),
                Math.Max(0, signe * facture.TotalHt),
                facture.Statut.ToString()));

            if (facture.TotalTva > 0)
            {
                rows.Add(Row(i++, facture.DateEmission, journal, facture.Numero, "436700",
                    "TVA collectee",
                    client,
                    Math.Max(0, -signe * facture.TotalTva),
                    Math.Max(0, signe * facture.TotalTva),
                    facture.Statut.ToString()));
            }
        }

        foreach (var paiement in paiements.OrderByDescending(p => p.DatePaiement).Take(30))
        {
            var piece = paiement.Reference ?? paiement.Id.ToString("N")[..10].ToUpperInvariant();
            rows.Add(Row(i++, paiement.DatePaiement, "BQ", piece, "532000", "Encaissement client", paiement.Banque ?? "Banque", paiement.Montant, 0, "Lettre"));
            rows.Add(Row(i++, paiement.DatePaiement, "BQ", piece, "411000", "Lettrage client", paiement.Banque ?? "Banque", 0, paiement.Montant, "Lettre"));
        }

        foreach (var tx in transactions.OrderByDescending(t => t.Date).Take(70))
        {
            var compte = string.IsNullOrWhiteSpace(tx.Compte) ? (tx.Type == TypeTransaction.Entree ? "532000" : "607000") : tx.Compte!;
            var journal = compte.StartsWith("6") ? "AC" : compte.StartsWith("2") || compte.StartsWith("4") ? "OD" : "BQ";
            rows.Add(Row(
                i++,
                tx.Date,
                journal,
                tx.Id.ToString("N")[..10].ToUpperInvariant(),
                compte,
                Clean(tx.Libelle),
                tx.TiersNom ?? tx.CategorieNom ?? "Interne",
                tx.Type == TypeTransaction.Sortie ? tx.Montant : 0,
                tx.Type == TypeTransaction.Entree ? tx.Montant : 0,
                tx.Statut == StatutTransaction.Justifiee ? "Validee" : "A controler"));

            if (tx.RecoverableVatAmount is > 0)
            {
                rows.Add(Row(
                    i++,
                    tx.Date,
                    "AC",
                    tx.Id.ToString("N")[..10].ToUpperInvariant(),
                    "436600",
                    "TVA deductible",
                    tx.TiersNom ?? "Fournisseur",
                    tx.RecoverableVatAmount.Value,
                    0,
                    tx.Statut == StatutTransaction.Justifiee ? "Validee" : "A controler"));
            }
        }

        return rows;
    }

    private static EcritureDto Row(
        int i,
        DateTime date,
        string journal,
        string piece,
        string compte,
        string libelle,
        string tiers,
        decimal debit,
        decimal credit,
        string statut)
        => new(
            Id: $"EC-{i:D4}",
            Date: DateOnly.FromDateTime(date).ToString("yyyy-MM-dd"),
            Journal: journal,
            Piece: piece,
            Compte: compte,
            Libelle: libelle,
            Tiers: tiers,
            Debit: Math.Round(debit, 3),
            Credit: Math.Round(credit, 3),
            Statut: statut);

    private static List<BalanceDto> BuildBalance(List<EcritureDto> ecritures, PlanCompteDto[] plan)
        => ecritures
            .GroupBy(e => e.Compte)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var debit = g.Sum(e => e.Debit);
                var credit = g.Sum(e => e.Credit);
                var label = plan.FirstOrDefault(p => p.Numero == g.Key);
                return new BalanceDto(
                    Compte: g.Key,
                    Intitule: label?.Intitule ?? "Compte",
                    Debit: Math.Round(debit, 3),
                    Credit: Math.Round(credit, 3),
                    Solde: Math.Round(Math.Abs(debit - credit), 3));
            })
            .ToList();

    private static List<GrandLivreDto> BuildGrandLivre(List<BalanceDto> balance, List<EcritureDto> ecritures)
        => balance
            .Select((b, i) => new GrandLivreDto(
                Id: $"GL-{i + 1:D3}",
                Compte: b.Compte,
                Intitule: b.Intitule,
                Mouvement: $"{ecritures.Count(e => e.Compte == b.Compte)} mouvements",
                Solde: b.Solde))
            .ToList();

    private static List<JournalDto> BuildJournaux(List<EcritureDto> ecritures)
        => ecritures
            .GroupBy(e => e.Journal)
            .OrderBy(g => g.Key)
            .Select(g => new JournalDto(
                Code: g.Key,
                Label: g.Key switch
                {
                    "VT" => "Journal ventes",
                    "AC" => "Journal achats",
                    "BQ" => "Journal banque",
                    "AV" => "Journal avoirs",
                    _ => "Operations diverses"
                },
                Role: "Controle legal et tracabilite",
                Total: Math.Round(g.Sum(e => e.Debit + e.Credit), 3),
                NbPieces: g.Count(),
                Controle: g.Any(e => e.Statut.Contains("control", StringComparison.OrdinalIgnoreCase)) ? "Pieces a controler" : "Controle OK"))
            .ToList();

    private static List<AnalytiqueDto> BuildAnalytique(List<Transaction> transactions)
        => transactions
            .GroupBy(t => string.IsNullOrWhiteSpace(t.CategorieNom) ? "GENERAL" : t.CategorieNom!)
            .OrderByDescending(g => g.Sum(t => t.Montant))
            .Take(8)
            .Select(g =>
            {
                var produits = g.Where(t => t.Type == TypeTransaction.Entree).Sum(t => t.Montant);
                var charges = g.Where(t => t.Type == TypeTransaction.Sortie).Sum(t => t.Montant);
                var marge = produits <= 0 ? 0 : Math.Round(((produits - charges) / produits) * 100, 1);

                return new AnalytiqueDto(
                    Code: new string(g.Key.Where(char.IsLetterOrDigit).Take(8).ToArray()).ToUpperInvariant(),
                    Label: g.Key,
                    Description: "Ventilation issue des transactions et justificatifs",
                    Produits: Math.Round(produits, 3),
                    Charges: Math.Round(charges, 3),
                    Marge: marge);
            })
            .ToList();

    private static PlanCompteDto[] PlanComptable() =>
    [
        new("101000", "Capital social", "Classe 1", "Capitaux propres", "Ouverture et mouvements du capital"),
        new("218200", "Materiel de transport", "Classe 2", "Immobilisations", "Vehicules et amortissements"),
        new("218300", "Materiel informatique", "Classe 2", "Immobilisations", "Serveurs et equipements"),
        new("401000", "Fournisseurs", "Classe 4", "Tiers", "Achats fournisseurs et OCR"),
        new("411000", "Clients", "Classe 4", "Tiers", "Factures, avoirs et lettrage"),
        new("436600", "TVA deductible", "Classe 4", "Fiscalite", "TVA achats"),
        new("436700", "TVA collectee", "Classe 4", "Fiscalite", "TVA ventes"),
        new("437100", "Retenue a la source", "Classe 4", "Fiscalite", "Modele 41"),
        new("438600", "FODEC a payer", "Classe 4", "Fiscalite", "FODEC 1%"),
        new("438800", "TCL a payer", "Classe 4", "Fiscalite", "Taxe collectivites locales"),
        new("532000", "Banques", "Classe 5", "Tresorerie", "Rapprochement bancaire"),
        new("607000", "Achats de marchandises", "Classe 6", "Charges", "Achats fournisseurs"),
        new("641100", "Salaires", "Classe 6", "Charges", "Paie"),
        new("681100", "Dotations aux amortissements", "Classe 6", "Charges", "Cloture immobilisations"),
        new("706000", "Prestations de services", "Classe 7", "Produits", "Services et abonnements"),
        new("707000", "Ventes de marchandises", "Classe 7", "Produits", "Facturation client")
    ];

    private static FiscalCalendarDto[] CalendrierFiscal() =>
    [
        new("D15", "28/mois", "Declaration mensuelle D15", "TVA, FODEC, TCL et timbre fiscal."),
        new("RS41", "28/mois", "Retenue a la source - Modele 41", "RS 1.5%, 3%, 5%, 10%, 15%."),
        new("CNSS", "15/trim.", "CNSS trimestrielle", "Masse salariale et cotisations."),
        new("IS", "25 avr./mai", "Declaration annuelle IS", "Depot annuel selon forme juridique.")
    ];

    private static ActifDto[] Actifs() =>
    [
        new("A1", "Serveur comptable", "Materiel informatique", "218300", 33, 12400m, 4092m, 8308m, "En service"),
        new("A2", "Vehicule commercial", "Transport", "218200", 20, 58000m, 11600m, 46400m, "En service")
    ];

    private static EtatComptableDto[] Etats() =>
    [
        new("BILAN", "ti-layout-board-split", "Bilan NCT", "Actifs, capitaux propres et passifs.", "NCT 01"),
        new("RESULTAT", "ti-report-money", "Etat de resultat", "Produits, charges et resultat net.", "NCT 01"),
        new("CASH", "ti-arrows-exchange", "Flux de tresorerie", "Exploitation, investissement, financement.", "NCT 07")
    ];

    private static ExerciceDto[] Exercices() =>
    [
        new("OPEN", "Ouverture exercice 2026", "Report a nouveau et reprise balance.", "Valide"),
        new("INV", "Inventaire annuel", "Stocks, immobilisations et provisions.", "En cours"),
        new("CLOSE", "Cloture et resultat", "Ecritures de cloture et archivage.", "A venir")
    ];

    private static string Clean(string value) => value.Replace("[COMPTA-SEED]", string.Empty).Trim();

    private sealed record ComptabiliteData(List<EcritureDto> Ecritures);
    private sealed record ComptabiliteDashboardDto(
        KpiDto[] Kpis,
        DashboardAlertDto[] Alertes,
        FiscalCalendarDto[] CalendrierFiscal,
        PlanCompteDto[] PlanComptable,
        List<EcritureDto> Ecritures,
        List<JournalDto> Journaux,
        List<BalanceDto> Balance,
        List<GrandLivreDto> GrandLivre,
        ActifDto[] Actifs,
        EtatComptableDto[] Etats,
        ExerciceDto[] Exercices,
        List<AnalytiqueDto> Analytique);
    private sealed record KpiDto(string Key, string Label, decimal Value, string Type, string Hint, string Icon, string Tone);
    private sealed record DashboardAlertDto(string Code, string Level, string Icon, string Title, string Text, string Deadline);
    private sealed record FiscalCalendarDto(string Code, string Echeance, string Label, string Detail);
    private sealed record PlanCompteDto(string Numero, string Intitule, string Classe, string Nature, string Usage);
    private sealed record EcritureDto(string Id, string Date, string Journal, string Piece, string Compte, string Libelle, string Tiers, decimal Debit, decimal Credit, string Statut);
    private sealed record JournalDto(string Code, string Label, string Role, decimal Total, int NbPieces, string Controle);
    private sealed record BalanceDto(string Compte, string Intitule, decimal Debit, decimal Credit, decimal Solde);
    private sealed record GrandLivreDto(string Id, string Compte, string Intitule, string Mouvement, decimal Solde);
    private sealed record ActifDto(string Id, string Label, string Famille, string Compte, int Taux, decimal Acquisition, decimal Dotation, decimal Vnc, string Statut);
    private sealed record EtatComptableDto(string Code, string Icon, string Label, string Description, string Norme);
    private sealed record ExerciceDto(string Code, string Label, string Detail, string Statut);
    private sealed record AnalytiqueDto(string Code, string Label, string Description, decimal Produits, decimal Charges, decimal Marge);
}
