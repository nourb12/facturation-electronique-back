using System.Globalization;
using System.Text;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/ecritures")]
[Produces("application/json")]
[Authorize]
public sealed class EcrituresController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] string? journal, [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin, [FromQuery] string? compte, [FromQuery] string? search, CancellationToken ct)
    {
        var ecritures = await ComptabiliteWorkflow.BuildEcrituresAsync(db, currentUser, ct);
        var rows = ecritures.Where(e =>
            (string.IsNullOrWhiteSpace(journal) || e.Journal.Equals(journal, StringComparison.OrdinalIgnoreCase)) &&
            (!dateDebut.HasValue || e.Date >= DateOnly.FromDateTime(dateDebut.Value)) &&
            (!dateFin.HasValue || e.Date <= DateOnly.FromDateTime(dateFin.Value)) &&
            (string.IsNullOrWhiteSpace(compte) || e.Compte.StartsWith(compte, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(search) || ComptabiliteWorkflow.Contains(e, search)));

        return Ok(rows);
    }

    [HttpPost]
    public IActionResult Creer([FromBody] CreerEcritureRequest request)
    {
        if (request.Lignes.Count < 2)
            return BadRequest(new { message = "Une écriture doit contenir au moins deux lignes." });

        var debit = request.Lignes.Sum(l => l.Debit);
        var credit = request.Lignes.Sum(l => l.Credit);
        if (Math.Abs(debit - credit) >= 0.001m)
            return BadRequest(new { message = "Le total débit doit être égal au total crédit." });

        var rows = request.Lignes.Select((line, index) => new EcritureDto(
            Id: $"OD-MANUELLE-{Guid.NewGuid():N}-{index + 1}",
            Date: DateOnly.FromDateTime(request.Date),
            Journal: string.IsNullOrWhiteSpace(request.Journal) ? "OD" : request.Journal.Trim().ToUpperInvariant(),
            Piece: request.Piece.Trim(),
            Compte: line.Compte.Trim(),
            Libelle: request.Libelle.Trim(),
            Tiers: request.Tiers?.Trim(),
            Debit: Math.Round(line.Debit, 3),
            Credit: Math.Round(line.Credit, 3),
            Statut: "Validée",
            FactureId: null,
            Source: "Manuelle")).ToList();

        return Created("/api/ecritures", rows);
    }

    [HttpGet("balance")]
    public async Task<IActionResult> Balance(CancellationToken ct)
        => Ok(ComptabiliteWorkflow.BuildBalance(await ComptabiliteWorkflow.BuildEcrituresAsync(db, currentUser, ct)));

    [HttpGet("grand-livre")]
    public async Task<IActionResult> GrandLivre([FromQuery] string? compte, [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin, CancellationToken ct)
    {
        var rows = (await ComptabiliteWorkflow.BuildEcrituresAsync(db, currentUser, ct))
            .Where(e =>
                (string.IsNullOrWhiteSpace(compte) || e.Compte.StartsWith(compte, StringComparison.OrdinalIgnoreCase)) &&
                (!dateDebut.HasValue || e.Date >= DateOnly.FromDateTime(dateDebut.Value)) &&
                (!dateFin.HasValue || e.Date <= DateOnly.FromDateTime(dateFin.Value)))
            .OrderBy(e => e.Compte)
            .ThenBy(e => e.Date)
            .ToList();

        var running = new Dictionary<string, decimal>();
        var grandLivre = rows.Select(e =>
        {
            var next = running.GetValueOrDefault(e.Compte) + e.Debit - e.Credit;
            running[e.Compte] = next;
            return new
            {
                e.Id,
                Date = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.Journal,
                e.Piece,
                e.Compte,
                Intitule = ComptabiliteWorkflow.AccountLabel(e.Compte),
                e.Libelle,
                e.Tiers,
                e.Debit,
                e.Credit,
                SoldeProgressif = Math.Round(next, 3),
                Lettrage = e.Compte.StartsWith("411") || e.Compte.StartsWith("401")
                    ? e.Statut.Contains("pay", StringComparison.OrdinalIgnoreCase) || e.Source == "Paiement" ? "Lettré" : "Non lettré"
                    : "-"
            };
        });

        return Ok(grandLivre);
    }

    [HttpGet("export/excel")]
    public async Task<FileResult> ExportExcel(CancellationToken ct)
        => ComptabiliteWorkflow.CsvFile("ecritures.csv", await ComptabiliteWorkflow.BuildEcrituresAsync(db, currentUser, ct));

    [HttpGet("export/pdf")]
    public async Task<FileResult> ExportPdf(CancellationToken ct)
        => ComptabiliteWorkflow.CsvFile("ecritures.pdf.csv", await ComptabiliteWorkflow.BuildEcrituresAsync(db, currentUser, ct));
}

[ApiController]
[Route("api/declarations")]
[Produces("application/json")]
[Authorize]
public sealed class DeclarationsController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("tva")]
    public async Task<IActionResult> Tva([FromQuery] int? mois, [FromQuery] int? annee, CancellationToken ct)
    {
        var entrepriseId = await ComptabiliteWorkflow.EntrepriseIdAsync(db, currentUser, ct);
        if (entrepriseId is null) return Ok(new { tvaCollectee = 0, tvaDeductible = 0, tvaNetteDue = 0 });

        var factures = await db.Factures.Where(f => f.EntrepriseId == entrepriseId).ToListAsync(ct);
        var transactions = await db.Transactions.Where(t => t.EntrepriseId == entrepriseId).ToListAsync(ct);
        factures = factures.Where(f => ComptabiliteWorkflow.InPeriod(f.DateEmission, mois, annee)).ToList();
        transactions = transactions.Where(t => ComptabiliteWorkflow.InPeriod(t.Date, mois, annee)).ToList();

        var collectee = factures.Where(f => f.TypeFacture != TypeFacture.Avoir).Sum(f => f.TotalTva);
        var deductible = transactions.Sum(t => t.RecoverableVatAmount ?? 0);
        var fodec = factures.Sum(f => Math.Round(f.TotalHt * 0.01m, 3));
        var tcl = factures.Sum(f => Math.Round(f.TotalHt * 0.002m, 3));

        return Ok(new
        {
            mois = mois ?? DateTime.UtcNow.Month,
            annee = annee ?? DateTime.UtcNow.Year,
            tvaCollectee = Math.Round(collectee, 3),
            tvaDeductible = Math.Round(deductible, 3),
            tvaNetteDue = Math.Round(Math.Max(0, collectee - deductible), 3),
            fodec,
            tcl,
            timbreFiscal = factures.Count,
            totalAPayer = Math.Round(Math.Max(0, collectee - deductible) + fodec + tcl + factures.Count, 3)
        });
    }

    [HttpGet("rs")]
    public async Task<IActionResult> RetenueSource([FromQuery] int? mois, [FromQuery] int? annee, CancellationToken ct)
    {
        var entrepriseId = await ComptabiliteWorkflow.EntrepriseIdAsync(db, currentUser, ct);
        if (entrepriseId is null) return Ok(new { total = 0, lignes = Array.Empty<object>() });

        var clients = await db.Clients.Where(c => c.EntrepriseId == entrepriseId).ToDictionaryAsync(c => c.Id, c => c.Nom, ct);
        var lignes = await db.Factures
            .Where(f => f.EntrepriseId == entrepriseId && f.AppliquerRS && f.MontantRS > 0)
            .ToListAsync(ct);

        lignes = lignes.Where(f => ComptabiliteWorkflow.InPeriod(f.DateEmission, mois, annee)).ToList();

        return Ok(new
        {
            total = Math.Round(lignes.Sum(f => f.MontantRS), 3),
            dateLimite = "28/mois",
            lignes = lignes.Select(f => new
            {
                fournisseur = clients.GetValueOrDefault(f.ClientId, "Client"),
                facture = f.Numero,
                montantBrut = f.BaseRS,
                taux = f.TauxRS,
                montantRs = f.MontantRS,
                naturePaiement = f.CodeRS ?? "Services"
            })
        });
    }

    [HttpGet("liasse-dgi")]
    public async Task<IActionResult> Liasse([FromQuery] int? annee, CancellationToken ct)
    {
        var ecritures = await ComptabiliteWorkflow.BuildEcrituresAsync(db, currentUser, ct);
        var filtered = ecritures.Where(e => !annee.HasValue || e.Date.Year == annee.Value).ToList();
        var produits = filtered.Where(e => e.Compte.StartsWith("7")).Sum(e => e.Credit - e.Debit);
        var charges = filtered.Where(e => e.Compte.StartsWith("6")).Sum(e => e.Debit - e.Credit);
        var resultat = produits - charges;

        return Ok(new
        {
            annee = annee ?? DateTime.UtcNow.Year,
            produits = Math.Round(produits, 3),
            charges = Math.Round(charges, 3),
            resultatNet = Math.Round(resultat, 3),
            isEstime = Math.Round(Math.Max(0, resultat) * 0.15m, 3)
        });
    }
}

[ApiController]
[Route("api/lettrage")]
[Produces("application/json")]
[Authorize]
public sealed class LettrageController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("non-lettres")]
    public async Task<IActionResult> NonLettres(CancellationToken ct)
    {
        var entrepriseId = await ComptabiliteWorkflow.EntrepriseIdAsync(db, currentUser, ct);
        if (entrepriseId is null) return Ok(Array.Empty<object>());

        var factures = await db.Factures
            .Where(f => f.EntrepriseId == entrepriseId && f.MontantRestant > 0 && f.Statut != StatutFacture.Annulee)
            .OrderBy(f => f.DateEcheance)
            .Select(f => new { f.Id, f.Numero, f.DateEcheance, f.TotalTtc, f.MontantPaye, f.MontantRestant, f.Statut })
            .ToListAsync(ct);

        return Ok(factures);
    }

    [HttpPost]
    public async Task<IActionResult> Lettrer([FromBody] LettrageRequest request, CancellationToken ct)
    {
        if (request.TransactionId is null || request.FactureId is null)
            return BadRequest(new { message = "La facture et la transaction sont obligatoires." });

        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == request.TransactionId, ct);
        if (transaction is null) return NotFound(new { message = "Transaction introuvable." });

        transaction.LierFacture(currentUser.UtilisateurId ?? Guid.Empty, request.FactureId.Value);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Lettrage enregistré.", transactionId = transaction.Id, factureId = request.FactureId });
    }
}

[ApiController]
[Route("api/rapprochement")]
[Produces("application/json")]
[Authorize]
public sealed class RapprochementController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("import")]
    public IActionResult Importer(IFormFile file)
        => Ok(new { message = "Relevé bancaire reçu.", fichier = file.FileName, lignesImportees = 0 });

    [HttpGet("ecarts")]
    public async Task<IActionResult> Ecarts(CancellationToken ct)
    {
        var entrepriseId = await ComptabiliteWorkflow.EntrepriseIdAsync(db, currentUser, ct);
        if (entrepriseId is null) return Ok(Array.Empty<object>());

        var rows = await db.Transactions
            .Where(t => t.EntrepriseId == entrepriseId && t.Compte != null && t.Compte.StartsWith("53") && t.BankMatchJson == null)
            .OrderByDescending(t => t.Date)
            .Select(t => new { t.Id, t.Date, t.Libelle, t.Montant, t.Type, statut = "Non rapprochée" })
            .ToListAsync(ct);

        return Ok(rows);
    }
}

[ApiController]
[Route("api/audit")]
[Produces("application/json")]
[Authorize]
public sealed class AuditController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] string? entite, [FromQuery] string? action, [FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin, CancellationToken ct)
    {
        var entrepriseId = await ComptabiliteWorkflow.EntrepriseIdAsync(db, currentUser, ct);
        if (entrepriseId is null) return Ok(Array.Empty<object>());

        var factureIds = await db.Factures.Where(f => f.EntrepriseId == entrepriseId).Select(f => f.Id).ToListAsync(ct);
        var historique = await db.HistoriqueFactures
            .Where(h => factureIds.Contains(h.FactureId))
            .OrderByDescending(h => h.CreeLe)
            .Take(200)
            .ToListAsync(ct);

        var rows = historique
            .Where(h => string.IsNullOrWhiteSpace(entite) || entite.Equals("Facture", StringComparison.OrdinalIgnoreCase))
            .Where(h => string.IsNullOrWhiteSpace(action) || h.Action.Contains(action, StringComparison.OrdinalIgnoreCase))
            .Where(h => !dateDebut.HasValue || h.CreeLe >= dateDebut.Value)
            .Where(h => !dateFin.HasValue || h.CreeLe <= dateFin.Value)
            .Select(h => new
            {
                h.Id,
                Date = h.CreeLe,
                UtilisateurId = h.EffectuePar,
                Entite = "Facture",
                h.FactureId,
                h.Action,
                AncienneValeur = h.AncienneValeur,
                NouvelleValeur = h.NouvelleValeur,
                Details = h.Details
            });

        return Ok(rows);
    }
}

internal static class ComptabiliteWorkflow
{
    internal static async Task<Guid?> EntrepriseIdAsync(ContextBaseDeDonnees db, ICurrentUserService currentUser, CancellationToken ct)
        => currentUser.EntrepriseId ?? await db.Entreprises.OrderByDescending(e => e.ModifieLe).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);

    internal static async Task<List<EcritureDto>> BuildEcrituresAsync(ContextBaseDeDonnees db, ICurrentUserService currentUser, CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(db, currentUser, ct);
        if (entrepriseId is null) return [];

        var factures = await db.Factures.Where(f => f.EntrepriseId == entrepriseId).OrderByDescending(f => f.DateEmission).ToListAsync(ct);
        var paiements = await db.Paiements.Where(p => p.EntrepriseId == entrepriseId).OrderByDescending(p => p.DatePaiement).ToListAsync(ct);
        var transactions = await db.Transactions.Where(t => t.EntrepriseId == entrepriseId).OrderByDescending(t => t.Date).ToListAsync(ct);
        var clients = await db.Clients.Where(c => c.EntrepriseId == entrepriseId).ToDictionaryAsync(c => c.Id, c => c.Nom, ct);

        var rows = new List<EcritureDto>();
        foreach (var facture in factures)
        {
            var journal = facture.TypeFacture == TypeFacture.Avoir ? "AV" : "VT";
            var signe = facture.TypeFacture == TypeFacture.Avoir ? -1 : 1;
            var client = clients.GetValueOrDefault(facture.ClientId, "Client");
            Add(rows, facture.DateEmission, journal, facture.Numero, "411000", "Créance client", client, Math.Max(0, signe * facture.TotalTtc), Math.Max(0, -signe * facture.TotalTtc), facture.Statut.ToString(), facture.Id, "Facture");
            Add(rows, facture.DateEmission, journal, facture.Numero, "707000", "Ventes facturées", client, Math.Max(0, -signe * facture.TotalHt), Math.Max(0, signe * facture.TotalHt), facture.Statut.ToString(), facture.Id, "Facture");
            if (facture.TotalTva > 0)
                Add(rows, facture.DateEmission, journal, facture.Numero, "436100", "TVA collectée", client, Math.Max(0, -signe * facture.TotalTva), Math.Max(0, signe * facture.TotalTva), facture.Statut.ToString(), facture.Id, "Facture");
            if (facture.MontantRS > 0)
                Add(rows, facture.DateEmission, journal, facture.Numero, "437100", "Retenue à la source", client, Math.Max(0, -signe * facture.MontantRS), Math.Max(0, signe * facture.MontantRS), facture.Statut.ToString(), facture.Id, "Facture");
        }

        foreach (var paiement in paiements)
        {
            var piece = paiement.Reference ?? paiement.Id.ToString("N")[..10].ToUpperInvariant();
            Add(rows, paiement.DatePaiement, "BQ", piece, "532000", "Encaissement client", paiement.Banque ?? "Banque", paiement.Montant, 0, "Lettré", paiement.FactureId, "Paiement");
            Add(rows, paiement.DatePaiement, "BQ", piece, "411000", "Lettrage client", paiement.Banque ?? "Banque", 0, paiement.Montant, "Lettré", paiement.FactureId, "Paiement");
        }

        foreach (var tx in transactions)
        {
            var compte = string.IsNullOrWhiteSpace(tx.Compte) ? tx.Type == TypeTransaction.Entree ? "532000" : "607000" : tx.Compte!;
            var journal = compte.StartsWith("6") ? "AC" : compte.StartsWith("4") ? "OD" : "BQ";
            Add(rows, tx.Date, journal, tx.Id.ToString("N")[..10].ToUpperInvariant(), compte, tx.Libelle, tx.TiersNom ?? tx.CategorieNom, tx.Type == TypeTransaction.Sortie ? tx.Montant : 0, tx.Type == TypeTransaction.Entree ? tx.Montant : 0, tx.Statut.ToString(), tx.FactureId, tx.Source.ToString());
            if (tx.RecoverableVatAmount is > 0)
                Add(rows, tx.Date, "AC", tx.Id.ToString("N")[..10].ToUpperInvariant(), "436600", "TVA déductible", tx.TiersNom, tx.RecoverableVatAmount.Value, 0, tx.Statut.ToString(), tx.FactureId, tx.Source.ToString());
        }

        return rows.OrderByDescending(r => r.Date).ThenBy(r => r.Piece).ToList();
    }

    internal static List<BalanceDto> BuildBalance(IEnumerable<EcritureDto> ecritures)
        => ecritures
            .GroupBy(e => e.Compte)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var debit = g.Sum(e => e.Debit);
                var credit = g.Sum(e => e.Credit);
                return new BalanceDto(g.Key, AccountLabel(g.Key), Math.Round(debit, 3), Math.Round(credit, 3), Math.Max(0, debit - credit), Math.Max(0, credit - debit));
            })
            .ToList();

    internal static bool Contains(EcritureDto e, string search)
    {
        var q = search.Trim();
        return e.Piece.Contains(q, StringComparison.OrdinalIgnoreCase)
            || e.Compte.Contains(q, StringComparison.OrdinalIgnoreCase)
            || e.Libelle.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (e.Tiers?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    internal static bool InPeriod(DateTime date, int? mois, int? annee)
        => (!mois.HasValue || date.Month == mois.Value) && (!annee.HasValue || date.Year == annee.Value);

    internal static string AccountLabel(string compte) => compte switch
    {
        "411000" => "Clients",
        "401000" => "Fournisseurs",
        "436100" => "TVA collectée",
        "436600" => "TVA déductible",
        "437100" => "Retenue à la source",
        "532000" => "Banques",
        "607000" => "Achats de marchandises",
        "707000" => "Ventes",
        _ => "Compte comptable"
    };

    internal static FileContentResult CsvFile(string fileName, IEnumerable<EcritureDto> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Date;Journal;Piece;Compte;Libelle;Tiers;Debit;Credit;Statut;Source");
        foreach (var row in rows)
        {
            csv.AppendLine($"{row.Date:yyyy-MM-dd};{row.Journal};{Escape(row.Piece)};{row.Compte};{Escape(row.Libelle)};{Escape(row.Tiers)};{row.Debit};{row.Credit};{Escape(row.Statut)};{Escape(row.Source)}");
        }

        return new FileContentResult(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv")
        {
            FileDownloadName = fileName
        };
    }

    private static void Add(List<EcritureDto> rows, DateTime date, string journal, string piece, string compte, string libelle, string? tiers, decimal debit, decimal credit, string statut, Guid? factureId, string source)
        => rows.Add(new EcritureDto($"EC-{rows.Count + 1:D5}", DateOnly.FromDateTime(date), journal, piece, compte, libelle, tiers, Math.Round(debit, 3), Math.Round(credit, 3), statut, factureId, source));

    private static string Escape(string? value) => (value ?? string.Empty).Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
}

public sealed record CreerEcritureRequest(DateTime Date, string Journal, string Piece, string Libelle, string? Tiers, List<CreerEcritureLigneRequest> Lignes);
public sealed record CreerEcritureLigneRequest(string Compte, decimal Debit, decimal Credit);
public sealed record LettrageRequest(Guid? FactureId, Guid? TransactionId, Guid? PaiementId);
public sealed record EcritureDto(string Id, DateOnly Date, string Journal, string Piece, string Compte, string Libelle, string? Tiers, decimal Debit, decimal Credit, string Statut, Guid? FactureId, string Source);
public sealed record BalanceDto(string Compte, string Intitule, decimal Debit, decimal Credit, decimal SoldeDebiteur, decimal SoldeCrediteur);
