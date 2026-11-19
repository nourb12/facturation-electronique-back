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

        var plan = await EnsurePlanComptableAsync(entrepriseId.Value, ct);
        var ecrituresManuelles = await db.EcrituresComptables.Where(e => e.EntrepriseId == entrepriseId).ToListAsync(ct);
        var ecritures = BuildEcritures(factures, paiements, transactions, clients, ecrituresManuelles);
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
            CalendrierFiscal: BuildCalendrierFiscal(DateTime.UtcNow, BuildFiscalSnapshot(factures, transactions)),
            PlanComptable: plan,
            Ecritures: ecritures,
            Journaux: BuildJournaux(ecritures),
            Balance: balance,
            GrandLivre: BuildGrandLivre(balance, ecritures),
            Actifs: await LoadActifsAsync(entrepriseId.Value, ct),
            Etats: BuildEtats(ecritures, balance),
            Exercices: await LoadExercicesAsync(entrepriseId.Value, ct),
            Analytique: BuildAnalytique(transactions)));
    }

    [HttpGet("plan-comptable")]
    public async Task<IActionResult> Plan(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        return entrepriseId is null ? NotFound() : Ok(await EnsurePlanComptableAsync(entrepriseId.Value, ct));
    }

    [HttpPost("plan-comptable")]
    public async Task<IActionResult> AjouterCompte([FromBody] CreerCompteRequest request, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        if (await db.ComptesComptables.AnyAsync(c => c.EntrepriseId == entrepriseId && c.Numero == request.Numero.Trim(), ct))
            return Conflict(new { message = "Ce compte existe déjà." });
        var compte = CompteComptable.Creer(entrepriseId.Value, request.Numero, request.Intitule, request.Classe, request.Nature, request.Usage);
        db.ComptesComptables.Add(compte);
        await db.SaveChangesAsync(ct);
        return Created($"/api/comptabilite/plan-comptable/{compte.Id}", ToPlanCompteDto(compte));
    }

    [HttpGet("calendrier-fiscal")]
    public async Task<IActionResult> Fiscal(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        var factures = await db.Factures.Where(f => f.EntrepriseId == entrepriseId).ToListAsync(ct);
        var transactions = await db.Transactions.Where(t => t.EntrepriseId == entrepriseId).ToListAsync(ct);
        return Ok(BuildCalendrierFiscal(DateTime.UtcNow, BuildFiscalSnapshot(factures, transactions)));
    }

    [HttpGet("actifs")]
    public async Task<IActionResult> Immobilisations(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        return entrepriseId is null ? NotFound() : Ok(await LoadActifsAsync(entrepriseId.Value, ct));
    }

    [HttpPost("actifs")]
    public async Task<IActionResult> CreerActif([FromBody] CreerActifRequest request, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        var actif = ActifComptable.Creer(entrepriseId.Value, request.Label, request.Famille, request.Compte, request.Taux, request.Acquisition,
            request.DateAcquisition ?? DateTime.UtcNow, request.Statut);
        db.ActifsComptables.Add(actif);
        await db.SaveChangesAsync(ct);
        return Created($"/api/comptabilite/actifs/{actif.Id}", ToActifDto(actif));
    }

    [HttpPatch("actifs/{id:guid}/taux")]
    public async Task<IActionResult> ModifierTauxActif(Guid id, [FromBody] ModifierTauxActifRequest request, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        var actif = await db.ActifsComptables.SingleOrDefaultAsync(a => a.Id == id && a.EntrepriseId == entrepriseId, ct);
        if (actif is null) return NotFound();
        actif.ChangerTaux(request.Taux);
        await db.SaveChangesAsync(ct);
        return Ok(ToActifDto(actif));
    }
    [HttpGet("etats")]
    public async Task<IActionResult> EtatsComptables(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        var data = await LoadAsync(ct);
        return Ok(BuildEtats(data.Ecritures, BuildBalance(data.Ecritures, await EnsurePlanComptableAsync(entrepriseId.Value, ct))));
    }
    [HttpGet("exercices")]
    public async Task<IActionResult> ExercicesComptables(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        return entrepriseId is null ? NotFound() : Ok(await LoadExercicesAsync(entrepriseId.Value, ct));
    }

    [HttpPost("exercices")]
    public async Task<IActionResult> OuvrirExercice([FromBody] OuvrirExerciceRequest request, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();
        if (await db.ExercicesComptables.AnyAsync(e => e.EntrepriseId == entrepriseId && e.Annee == request.Annee, ct))
            return Conflict(new { message = "Cet exercice existe déjà." });
        var exercice = ExerciceComptable.Creer(entrepriseId.Value, request.Annee);
        db.ExercicesComptables.Add(exercice);
        await db.SaveChangesAsync(ct);
        return Created($"/api/comptabilite/exercices/{exercice.Id}", ToExerciceDto(exercice));
    }

    [HttpPost("exercices/{id:guid}/cloturer")]
    public async Task<IActionResult> CloturerExercice(Guid id, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        var exercice = await db.ExercicesComptables.SingleOrDefaultAsync(e => e.Id == id && e.EntrepriseId == entrepriseId, ct);
        if (exercice is null) return NotFound();
        exercice.Cloturer();
        await db.SaveChangesAsync(ct);
        return Ok(ToExerciceDto(exercice));
    }

    [HttpGet("ecritures")]
    public async Task<IActionResult> Ecritures(CancellationToken ct) => Ok((await LoadAsync(ct)).Ecritures);

    [HttpGet("journaux")]
    public async Task<IActionResult> Journaux(CancellationToken ct) => Ok(BuildJournaux((await LoadAsync(ct)).Ecritures));

    [HttpGet("balance")]
    public async Task<IActionResult> Balance(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        return Ok(BuildBalance((await LoadAsync(ct)).Ecritures, entrepriseId is null ? [] : await EnsurePlanComptableAsync(entrepriseId.Value, ct)));
    }

    [HttpGet("grand-livre")]
    public async Task<IActionResult> GrandLivre(CancellationToken ct)
    {
        var data = await LoadAsync(ct);
        var entrepriseId = await EntrepriseIdAsync(ct);
        var balance = BuildBalance(data.Ecritures, entrepriseId is null ? [] : await EnsurePlanComptableAsync(entrepriseId.Value, ct));
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
        var ecrituresManuelles = await db.EcrituresComptables.Where(e => e.EntrepriseId == entrepriseId).ToListAsync(ct);
        return new(BuildEcritures(factures, paiements, transactions, clients, ecrituresManuelles));
    }

    private Task<Guid?> EntrepriseIdAsync(CancellationToken ct)
        => Task.FromResult(currentUser.EntrepriseId);

    private static List<EcritureDto> BuildEcritures(
        List<Facture> factures,
        List<Paiement> paiements,
        List<Transaction> transactions,
        Dictionary<Guid, string> clients,
        IEnumerable<EcritureComptable>? ecrituresManuelles = null)
    {
        var rows = new List<EcritureDto>();
        var i = 1;

        // Seules les pièces émises sont comptabilisées : ni devis, ni proforma, ni brouillon, ni facture annulée.
        var piecesComptables = factures.Where(f =>
            f.TypeFacture is TypeFacture.Facture or TypeFacture.Avoir
            && f.Statut is not (StatutFacture.Brouillon or StatutFacture.Annulee));

        foreach (var facture in piecesComptables.OrderByDescending(f => f.DateEmission).Take(500))
        {
            var client = clients.GetValueOrDefault(facture.ClientId, "Client");
            var journal = facture.TypeFacture == TypeFacture.Avoir ? "AV" : "VT";
            var signe = facture.TypeFacture == TypeFacture.Avoir ? -1 : 1;
            var creance = facture.TotalTtc + facture.TimbreFiscal;

            rows.Add(Row(i++, facture.DateEmission, journal, facture.Numero, "411000",
                facture.TypeFacture == TypeFacture.Avoir ? "Avoir client" : "Creance client",
                client,
                Math.Max(0, signe * creance),
                Math.Max(0, -signe * creance),
                facture.Statut.ToString()));

            if (facture.TimbreFiscal > 0)
            {
                rows.Add(Row(i++, facture.DateEmission, journal, facture.Numero, "438900",
                    "Droit de timbre",
                    client,
                    Math.Max(0, -signe * facture.TimbreFiscal),
                    Math.Max(0, signe * facture.TimbreFiscal),
                    facture.Statut.ToString()));
            }

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

        foreach (var paiement in paiements.OrderByDescending(p => p.DatePaiement).Take(500))
        {
            var piece = paiement.Reference ?? paiement.Id.ToString("N")[..10].ToUpperInvariant();
            rows.Add(Row(i++, paiement.DatePaiement, "BQ", piece, "532000", "Encaissement client", paiement.Banque ?? "Banque", paiement.Montant, 0, "Lettre"));
            rows.Add(Row(i++, paiement.DatePaiement, "BQ", piece, "411000", "Lettrage client", paiement.Banque ?? "Banque", 0, paiement.Montant, "Lettre"));
        }

        foreach (var tx in transactions.OrderByDescending(t => t.Date).Take(500))
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

        foreach (var ecriture in ecrituresManuelles ?? [])
        {
            rows.Add(Row(i++, ecriture.Date, ecriture.Journal, ecriture.Piece, ecriture.Compte, ecriture.Libelle,
                ecriture.Tiers ?? string.Empty, ecriture.Debit, ecriture.Credit, ecriture.Statut));
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

    private static List<BalanceDto> BuildBalance(List<EcritureDto> ecritures, IReadOnlyList<PlanCompteDto> plan)
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

    private static PlanCompteDto[] PlanComptableParDefaut() =>
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
        new("438900", "Droit de timbre a reverser", "Classe 4", "Fiscalite", "Timbre fiscal sur factures"),
        new("532000", "Banques", "Classe 5", "Tresorerie", "Rapprochement bancaire"),
        new("607000", "Achats de marchandises", "Classe 6", "Charges", "Achats fournisseurs"),
        new("641100", "Salaires", "Classe 6", "Charges", "Paie"),
        new("681100", "Dotations aux amortissements", "Classe 6", "Charges", "Cloture immobilisations"),
        new("706000", "Prestations de services", "Classe 7", "Produits", "Services et abonnements"),
        new("707000", "Ventes de marchandises", "Classe 7", "Produits", "Facturation client")
    ];

    private static FiscalCalendarDto[] BuildCalendrierFiscal(DateTime date, FiscalSnapshot fiscal) =>
    [
        new("D15", new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)).ToString("dd/MM/yyyy"), "Déclaration mensuelle D15", "Montant calculé depuis les factures et achats de la période.", fiscal.TvaNette),
        new("RS41", new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)).ToString("dd/MM/yyyy"), "Retenue à la source - État 257", "Montant calculé depuis les factures fournisseur avec retenue.", fiscal.RetenueSource),
        new("CNSS", "15 du trimestre", "Déclaration CNSS", "Échéance liée aux salaires validés.", 0),
        new("IS", $"25/04/{date.Year + 1}", "Déclaration annuelle IS", "Résultat annuel estimé depuis les écritures validées.", fiscal.Resultat)
    ];

    private static EtatComptableDto[] BuildEtats(IReadOnlyList<EcritureDto> ecritures, IReadOnlyList<BalanceDto> balance) =>
    [
        new("BILAN", "ti-layout-board-split", "Bilan NCT", "Actifs, capitaux propres et passifs calculés depuis la balance.", "NCT 01",
            Math.Round(balance.Where(row => row.Compte.StartsWith("1") || row.Compte.StartsWith("2") || row.Compte.StartsWith("3") || row.Compte.StartsWith("4") || row.Compte.StartsWith("5")).Sum(row => row.Solde), 3)),
        new("RESULTAT", "ti-report-money", "État de résultat", "Produits, charges et résultat net calculés depuis les écritures.", "NCT 01",
            Math.Round(ecritures.Where(row => row.Compte.StartsWith("7")).Sum(row => row.Credit - row.Debit) - ecritures.Where(row => row.Compte.StartsWith("6")).Sum(row => row.Debit - row.Credit), 3)),
        new("CASH", "ti-arrows-exchange", "Flux de trésorerie", "Encaissements et décaissements calculés depuis les comptes de trésorerie.", "NCT 07",
            Math.Round(ecritures.Where(row => row.Compte.StartsWith("5")).Sum(row => row.Debit - row.Credit), 3))
    ];

    private static FiscalSnapshot BuildFiscalSnapshot(IReadOnlyList<Facture> factures, IReadOnlyList<Transaction> transactions)
    {
        var collectee = factures.Where(f => f.TypeFacture != TypeFacture.Avoir).Sum(f => f.TotalTva);
        var deductible = transactions.Sum(t => t.RecoverableVatAmount ?? 0);
        var resultat = factures.Where(f => f.TypeFacture != TypeFacture.Avoir).Sum(f => f.TotalHt)
            - transactions.Where(t => t.Type == TypeTransaction.Sortie).Sum(t => t.Montant);
        return new FiscalSnapshot(Math.Round(Math.Max(0, collectee - deductible), 3), 0, Math.Round(resultat, 3));
    }


    private async Task<List<PlanCompteDto>> EnsurePlanComptableAsync(Guid entrepriseId, CancellationToken ct)
    {
        var existing = await db.ComptesComptables.Where(c => c.EntrepriseId == entrepriseId).OrderBy(c => c.Numero).ToListAsync(ct);
        if (existing.Count == 0)
        {
            db.ComptesComptables.AddRange(PlanComptableParDefaut().Select(c => CompteComptable.Creer(entrepriseId, c.Numero, c.Intitule, c.Classe, c.Nature, c.Usage)));
            await db.SaveChangesAsync(ct);
            existing = await db.ComptesComptables.Where(c => c.EntrepriseId == entrepriseId).OrderBy(c => c.Numero).ToListAsync(ct);
        }
        return existing.Select(ToPlanCompteDto).ToList();
    }

    private async Task<List<ActifDto>> LoadActifsAsync(Guid entrepriseId, CancellationToken ct)
        => (await db.ActifsComptables.Where(a => a.EntrepriseId == entrepriseId).OrderByDescending(a => a.DateAcquisition).ToListAsync(ct)).Select(ToActifDto).ToList();

    private async Task<List<ExerciceDto>> LoadExercicesAsync(Guid entrepriseId, CancellationToken ct)
    {
        var exercices = await db.ExercicesComptables.Where(e => e.EntrepriseId == entrepriseId).OrderByDescending(e => e.Annee).ToListAsync(ct);
        if (exercices.Count == 0)
        {
            var current = ExerciceComptable.Creer(entrepriseId, DateTime.UtcNow.Year);
            db.ExercicesComptables.Add(current);
            await db.SaveChangesAsync(ct);
            exercices = [current];
        }
        return exercices.Select(ToExerciceDto).ToList();
    }

    private static PlanCompteDto ToPlanCompteDto(CompteComptable compte) => new(compte.Numero, compte.Intitule, compte.Classe, compte.Nature, compte.Usage);
    private static ActifDto ToActifDto(ActifComptable actif)
    {
        var dotation = Math.Round(actif.ValeurAcquisition * actif.TauxAmortissement / 100m, 3);
        return new(actif.Id.ToString(), actif.Libelle, actif.Famille, actif.Compte, (int)actif.TauxAmortissement, actif.ValeurAcquisition, dotation,
            Math.Round(Math.Max(0, actif.ValeurAcquisition - dotation), 3), actif.Statut);
    }
    private static ExerciceDto ToExerciceDto(ExerciceComptable exercice) => new(exercice.Id.ToString(), $"Exercice {exercice.Annee}",
        $"Du {exercice.DateDebut:dd/MM/yyyy} au {exercice.DateFin:dd/MM/yyyy}.", exercice.Statut);

    private static string Clean(string value) => value.Replace("[COMPTA-SEED]", string.Empty).Trim();

    private sealed record ComptabiliteData(List<EcritureDto> Ecritures);
    private sealed record ComptabiliteDashboardDto(
        KpiDto[] Kpis,
        DashboardAlertDto[] Alertes,
        FiscalCalendarDto[] CalendrierFiscal,
        IReadOnlyList<PlanCompteDto> PlanComptable,
        List<EcritureDto> Ecritures,
        List<JournalDto> Journaux,
        List<BalanceDto> Balance,
        List<GrandLivreDto> GrandLivre,
        IReadOnlyList<ActifDto> Actifs,
        EtatComptableDto[] Etats,
        IReadOnlyList<ExerciceDto> Exercices,
        List<AnalytiqueDto> Analytique);
    private sealed record KpiDto(string Key, string Label, decimal Value, string Type, string Hint, string Icon, string Tone);
    private sealed record DashboardAlertDto(string Code, string Level, string Icon, string Title, string Text, string Deadline);
    private sealed record FiscalCalendarDto(string Code, string Echeance, string Label, string Detail, decimal Montant);
    private sealed record PlanCompteDto(string Numero, string Intitule, string Classe, string Nature, string Usage);
    private sealed record EcritureDto(string Id, string Date, string Journal, string Piece, string Compte, string Libelle, string Tiers, decimal Debit, decimal Credit, string Statut);
    private sealed record JournalDto(string Code, string Label, string Role, decimal Total, int NbPieces, string Controle);
    private sealed record BalanceDto(string Compte, string Intitule, decimal Debit, decimal Credit, decimal Solde);
    private sealed record GrandLivreDto(string Id, string Compte, string Intitule, string Mouvement, decimal Solde);
    private sealed record ActifDto(string Id, string Label, string Famille, string Compte, int Taux, decimal Acquisition, decimal Dotation, decimal Vnc, string Statut);
    private sealed record EtatComptableDto(string Code, string Icon, string Label, string Description, string Norme, decimal Montant);
    private sealed record ExerciceDto(string Code, string Label, string Detail, string Statut);
    private sealed record AnalytiqueDto(string Code, string Label, string Description, decimal Produits, decimal Charges, decimal Marge);
    private sealed record FiscalSnapshot(decimal TvaNette, decimal RetenueSource, decimal Resultat);
}

public sealed record CreerCompteRequest(string Numero, string Intitule, string Classe, string Nature, string Usage);
public sealed record CreerActifRequest(string Label, string Famille, string Compte, decimal Taux, decimal Acquisition, DateTime? DateAcquisition, string? Statut);
public sealed record ModifierTauxActifRequest(decimal Taux);
public sealed record OuvrirExerciceRequest(int Annee);
