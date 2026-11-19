using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Produces("application/json")]
[Authorize]
public sealed class DocumentsController(
    ContextBaseDeDonnees db,
    IFactureService factureService,
    ICurrentUserService currentUser) : ControllerBase
{
    public sealed record CreatePurchaseDocumentRequest(
        string Type,
        string Numero,
        string Fournisseur,
        decimal Total,
        DateTime Date,
        DateTime? Echeance = null,
        string? Statut = null,
        string? Reference = null,
        string? Notes = null);

    public sealed record UpdatePurchaseDocumentRequest(
        string? Numero = null,
        string? Fournisseur = null,
        decimal? Total = null,
        DateTime? Date = null,
        DateTime? Echeance = null,
        string? Statut = null,
        string? Reference = null,
        string? Notes = null);

    public sealed record DocumentFluxDto(
        string Id,
        string Type,
        string Numero,
        string Client,
        decimal Total,
        string Statut,
        string Date,
        string? Echeance,
        string? LinkedTo,
        string? ConvertedTo,
        string[] History,
        string Source,
        string? ClientId = null);

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();

        var clients = await db.Clients
            .Where(c => c.EntrepriseId == entrepriseId)
            .ToDictionaryAsync(c => c.Id, c => c.Nom, ct);
        var fournisseurs = await db.Fournisseurs.Where(f => f.EntrepriseId == entrepriseId).ToDictionaryAsync(f => f.Id, f => f.Nom, ct);

        var factures = await db.Factures
            .Include(f => f.Historique)
            .Where(f => f.EntrepriseId == entrepriseId)
            .OrderByDescending(f => f.DateEmission)
            .Take(120)
            .ToListAsync(ct);

        var paiements = await db.Paiements
            .Where(p => p.EntrepriseId == entrepriseId)
            .OrderByDescending(p => p.DatePaiement)
            .Take(80)
            .ToListAsync(ct);

        var transactions = await db.Transactions
            .Where(t => t.EntrepriseId == entrepriseId && (t.Source == DocumentSource.MobileApp || t.DocumentType != null))
            .OrderByDescending(t => t.Date)
            .Take(80)
            .ToListAsync(ct);

        var scans = await db.ScannedDocuments
            .Where(s => s.EntrepriseId == entrepriseId)
            .OrderByDescending(s => s.CreeLe)
            .Take(40)
            .ToListAsync(ct);

        var documentsCommerciaux = await db.DocumentsCommerciaux
            .Where(d => d.EntrepriseId == entrepriseId)
            .OrderByDescending(d => d.DateDocument)
            .Take(120)
            .ToListAsync(ct);
        var facturesFournisseur = await db.FacturesFournisseur.Where(f => f.EntrepriseId == entrepriseId).OrderByDescending(f => f.DateFacture).Take(120).ToListAsync(ct);
        var paiementsFournisseur = await db.PaiementsFournisseur.Where(p => p.EntrepriseId == entrepriseId).OrderByDescending(p => p.DatePaiement).Take(120).ToListAsync(ct);

        var factureById = factures.ToDictionary(f => f.Id, f => f);
        var documentsConvertis = documentsCommerciaux
            .Where(d => d.DocumentOrigineId.HasValue)
            .GroupBy(d => d.DocumentOrigineId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreeLe).First());
        var rows = new List<DocumentFluxDto>();

        rows.AddRange(factures.Select(f => FromFacture(f, clients)));
        rows.AddRange(paiements.Select(p => FromPaiement(p, factureById, clients)));
        rows.AddRange(transactions.Where(t => t.FactureId is null).Select(FromTransaction));
        rows.AddRange(scans.Where(s => s.TransactionId is null).Select(FromScan));
        rows.AddRange(documentsCommerciaux.Select(d => FromDocumentCommercial(d, clients, documentsConvertis, factureById)));
        rows.AddRange(facturesFournisseur.Select(f => FromFactureFournisseur(f, fournisseurs)));
        rows.AddRange(paiementsFournisseur.Select(p => FromPaiementFournisseur(p, facturesFournisseur, fournisseurs)));

        return Ok(new
        {
            generatedAt = DateTime.UtcNow,
            documents = rows
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderByDescending(d => d.Date)
                .ThenBy(d => d.Numero)
                .ToArray()
        });
    }

    [HttpPost("{id}/convertir")]
    [Authorize(Policy = "Ventes")]
    [ProducesResponseType(typeof(DocumentFluxDto), 200)]
    public async Task<IActionResult> Convertir(string id, CancellationToken ct)
    {
        if (currentUser.UtilisateurId is null)
            return Problem(title: "Utilisateur invalide", statusCode: 401);

        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();

        var decoded = Uri.UnescapeDataString(id);
        var parts = decoded.Split(':', 2);
        if (parts.Length != 2 || parts[0] != "facture" || !Guid.TryParse(parts[1], out var factureId))
            return BadRequest(new { message = "Seuls les devis et proformas backend peuvent etre convertis automatiquement." });

        var created = await factureService.ConvertirEnFactureAsync(
            factureId,
            entrepriseId.Value,
            currentUser.UtilisateurId.Value,
            new ConvertirFactureRequest(Reference: $"Convertie depuis flux documents {DateTime.UtcNow:yyyy-MM-dd}"),
            ct);

        return Ok(new DocumentFluxDto(
            Id: $"facture:{created.Id}",
            Type: "facture",
            Numero: created.Numero,
            Client: created.ClientNom,
            Total: created.TotalTtc,
            Statut: MapFactureStatus(created.Statut, created.EstEnRetard),
            Date: created.DateEmission.ToString("yyyy-MM-dd"),
            Echeance: created.DateEcheance.ToString("yyyy-MM-dd"),
            LinkedTo: created.FactureOrigineId?.ToString(),
            ConvertedTo: null,
            History: ["Document converti depuis le flux documents", $"Origine: {decoded}"],
            Source: "Backend"));
    }

    [HttpPost("achats")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(DocumentFluxDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreerDocumentAchat([FromBody] CreatePurchaseDocumentRequest request, CancellationToken ct)
    {
        var guard = EntrepriseUtilisateurRequis(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        string type;
        try
        {
            type = NormalizePurchaseType(request.Type);
        }
        catch (ArgumentException exception)
        {
            return Problem(title: "Document invalide", detail: exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        if (string.IsNullOrWhiteSpace(request.Numero) || string.IsNullOrWhiteSpace(request.Fournisseur))
            return Problem(title: "Document invalide", detail: "Le numero et le fournisseur sont requis.", statusCode: StatusCodes.Status400BadRequest);

        var transaction = Transaction.Creer(
            entrepriseId,
            utilisateurId,
            request.Date,
            request.Numero,
            request.Total,
            TypeTransaction.Sortie,
            tiersNom: request.Fournisseur,
            categorieNom: "Achats",
            description: BuildPurchaseDescription(request.Reference, request.Notes),
            source: DocumentSource.Web,
            statut: MapPurchaseStatus(request.Statut));

        transaction.EnregistrerContexteRevision(
            utilisateurId,
            type,
            null,
            "[]",
            "[]",
            null,
            request.Fournisseur,
            null);

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);
        return Created($"/api/documents/achats/transaction:{transaction.Id}", FromTransaction(transaction));
    }

    [HttpPut("achats/{id}")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(DocumentFluxDto), 200)]
    public async Task<IActionResult> MettreAJourDocumentAchat(string id, [FromBody] UpdatePurchaseDocumentRequest request, CancellationToken ct)
    {
        var guard = EntrepriseUtilisateurRequis(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var transaction = await FindPurchaseTransactionAsync(id, entrepriseId, ct);
        if (transaction is null) return NotFound();

        transaction.MettreAJour(
            utilisateurId,
            date: request.Date,
            libelle: request.Numero,
            montant: request.Total,
            tiersNom: request.Fournisseur,
            description: request.Reference is null && request.Notes is null
                ? null
                : BuildPurchaseDescription(request.Reference, request.Notes),
            statut: request.Statut is null ? null : MapPurchaseStatus(request.Statut));

        await db.SaveChangesAsync(ct);
        return Ok(FromTransaction(transaction));
    }

    [HttpDelete("achats/{id}")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SupprimerDocumentAchat(string id, CancellationToken ct)
    {
        var guard = EntrepriseUtilisateurRequis(out var entrepriseId, out _);
        if (guard is not null) return guard;

        var transaction = await FindPurchaseTransactionAsync(id, entrepriseId, ct);
        if (transaction is null) return NotFound();

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<Guid?> EntrepriseIdAsync(CancellationToken ct)
        => Task.FromResult(currentUser.EntrepriseId);

    private IActionResult? EntrepriseUtilisateurRequis(out Guid entrepriseId, out Guid utilisateurId)
    {
        entrepriseId = Guid.Empty;
        utilisateurId = Guid.Empty;
        if (currentUser.UtilisateurId is null)
            return Problem(title: "Utilisateur invalide", statusCode: StatusCodes.Status401Unauthorized);

        var currentEntreprise = currentUser.EntrepriseId;
        if (currentEntreprise is null)
            return Problem(title: "Entreprise non rattachee", statusCode: StatusCodes.Status403Forbidden);

        entrepriseId = currentEntreprise.Value;
        utilisateurId = currentUser.UtilisateurId.Value;
        return null;
    }

    private async Task<Transaction?> FindPurchaseTransactionAsync(string id, Guid entrepriseId, CancellationToken ct)
    {
        var decoded = Uri.UnescapeDataString(id);
        var rawId = decoded.StartsWith("transaction:", StringComparison.OrdinalIgnoreCase)
            ? decoded["transaction:".Length..]
            : decoded;
        if (!Guid.TryParse(rawId, out var transactionId)) return null;

        return await db.Transactions.SingleOrDefaultAsync(
            t => t.Id == transactionId && t.EntrepriseId == entrepriseId && t.Type == TypeTransaction.Sortie && t.DocumentType != null,
            ct);
    }

    private static string NormalizePurchaseType(string? type)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "bon_commande" => "bon_commande",
            "bon_reception" or "bon_livraison" => "bon_reception",
            "prestation" or "prestation_service" => "prestation",
            _ => throw new ArgumentException("Type de document achat non pris en charge.")
        };
    }

    private static StatutTransaction MapPurchaseStatus(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "confirmé" or "confirme" or "livré" or "livre" => StatutTransaction.Justifiee,
            "en cours" or "reçu" or "recu" or "préparé" or "prepare" => StatutTransaction.EnAttente,
            _ => StatutTransaction.NonJustifiee
        };

    private static string? BuildPurchaseDescription(string? reference, string? notes)
    {
        var parts = new[] { reference, notes }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim());
        var value = string.Join(" | ", parts);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DocumentFluxDto FromFacture(Facture f, Dictionary<Guid, string> clients)
    {
        var type = f.TypeFacture switch
        {
            TypeFacture.Avoir => "avoir",
            TypeFacture.Proforma => "devis",
            TypeFacture.Devis => "devis",
            _ => "facture"
        };

        var linkedTo = f.FactureOrigineId is null ? null : $"facture:{f.FactureOrigineId}";
        var history = f.Historique
            .OrderBy(h => h.CreeLe)
            .Select(h => string.IsNullOrWhiteSpace(h.Details) ? h.Action : $"{h.Action}: {h.Details}")
            .DefaultIfEmpty($"Document {f.Numero} cree dans le backend")
            .ToArray();

        return new DocumentFluxDto(
            Id: $"facture:{f.Id}",
            Type: type,
            Numero: f.Numero,
            Client: clients.GetValueOrDefault(f.ClientId, "Client"),
            Total: f.TotalTtc,
            Statut: f.TypeFacture == TypeFacture.Avoir ? MapAvoirStatus(f.Statut) : MapFactureStatus(f.Statut.ToString(), f.EstEnRetard),
            Date: f.DateEmission.ToString("yyyy-MM-dd"),
            Echeance: f.DateEcheance.ToString("yyyy-MM-dd"),
            LinkedTo: linkedTo,
            ConvertedTo: null,
            History: history,
            Source: "Backend");
    }

    private static DocumentFluxDto FromPaiement(Paiement p, Dictionary<Guid, Facture> factures, Dictionary<Guid, string> clients)
    {
        factures.TryGetValue(p.FactureId, out var facture);
        var client = facture is null ? "Client" : clients.GetValueOrDefault(facture.ClientId, "Client");
        var reference = string.IsNullOrWhiteSpace(p.Reference) ? p.Id.ToString("N")[..8].ToUpperInvariant() : p.Reference;

        return new DocumentFluxDto(
            Id: $"paiement:{p.Id}",
            Type: "recu_paiement",
            Numero: $"REC-{p.DatePaiement:yyyy}-{reference}",
            Client: client,
            Total: p.Montant,
            Statut: "Envoyé",
            Date: p.DatePaiement.ToString("yyyy-MM-dd"),
            Echeance: null,
            LinkedTo: facture?.Numero,
            ConvertedTo: null,
            History: [$"Paiement enregistre par {p.Mode}", $"Reference: {reference}"],
            Source: "Backend");
    }

    private static DocumentFluxDto FromTransaction(Transaction t)
    {
        var type = MapDocumentType(t.DocumentType, t.Type);
        var status = t.Statut == StatutTransaction.Justifiee ? "Confirmé" : t.Statut == StatutTransaction.EnAttente ? "En cours" : "Reçu";
        var isPurchaseWorkflow = type is "bon_commande" or "bon_reception" or "prestation";
        var numero = isPurchaseWorkflow
            ? t.Libelle
            : t.JustificatifNomFichier ?? $"{(t.Type == TypeTransaction.Sortie ? "PAY-P" : "DOC")}-{t.Date:yyyy}-{t.Id.ToString("N")[..6].ToUpperInvariant()}";

        return new DocumentFluxDto(
            Id: $"transaction:{t.Id}",
            Type: type,
            Numero: numero,
            Client: t.TiersNom ?? t.CategorieNom ?? "Tiers",
            Total: t.Montant,
            Statut: status,
            Date: t.Date.ToString("yyyy-MM-dd"),
            Echeance: null,
            LinkedTo: null,
            ConvertedTo: null,
            History: [$"Document {t.DocumentType ?? "transaction"} importe", $"Statut transaction: {t.Statut}"],
            Source: t.Source == DocumentSource.MobileApp ? "Mobile" : "Backend");
    }

    private static DocumentFluxDto FromScan(ScannedDocument s)
    {
        var status = s.Status == ScannedDocumentStatus.LinkedToTransaction
            ? "Confirmé"
            : s.Status == ScannedDocumentStatus.MobileReviewed ? "En cours" : "Reçu";

        return new DocumentFluxDto(
            Id: $"scan:{s.Id}",
            Type: MapDocumentType(s.DocumentType, TypeTransaction.Sortie),
            Numero: s.FileName,
            Client: "Document scanné",
            Total: 0,
            Statut: status,
            Date: s.CreeLe.ToString("yyyy-MM-dd"),
            Echeance: null,
            LinkedTo: s.TransactionId?.ToString(),
            ConvertedTo: null,
            History: [$"Scan {s.DocumentType}", $"Confiance OCR: {s.OverallConfidence}%"],
            Source: s.Source == DocumentSource.MobileApp ? "Mobile" : "Backend");
    }

    private static DocumentFluxDto FromDocumentCommercial(
        DocumentCommercial d,
        Dictionary<Guid, string> clients,
        Dictionary<Guid, DocumentCommercial> documentsConvertis,
        Dictionary<Guid, Facture> factures)
        => new(
            Id: $"commercial:{d.Id}",
            Type: d.Type,
            Numero: d.Numero,
            Client: clients.GetValueOrDefault(d.ClientId, "Client"),
            Total: d.TotalTtc,
            Statut: d.Statut,
            Date: d.DateDocument.ToString("yyyy-MM-dd"),
            Echeance: d.DateEcheance?.ToString("yyyy-MM-dd"),
            LinkedTo: d.DocumentOrigineId is null ? null : $"commercial:{d.DocumentOrigineId}",
            ConvertedTo: d.FactureCreeeId is not null && factures.TryGetValue(d.FactureCreeeId.Value, out var facture)
                ? facture.Numero
                : documentsConvertis.TryGetValue(d.Id, out var enfant) ? enfant.Numero : null,
            History: ["Document commercial enregistré", d.FactureCreeeId is null ? $"Statut : {d.Statut}" : "Facture créée depuis ce bon de livraison"],
            Source: "Backend",
            ClientId: d.ClientId.ToString());

    private static DocumentFluxDto FromFactureFournisseur(FactureFournisseur f, Dictionary<Guid, string> fournisseurs) => new(
        Id: $"facture-fournisseur:{f.Id}", Type: "facture_fournisseur", Numero: f.Numero,
        Client: fournisseurs.GetValueOrDefault(f.FournisseurId, "Fournisseur"), Total: f.NetAPayer, Statut: f.Statut,
        Date: f.DateFacture.ToString("yyyy-MM-dd"), Echeance: f.DateEcheance.ToString("yyyy-MM-dd"), LinkedTo: f.TransactionOrigineId?.ToString(), ConvertedTo: null,
        History: [$"Facture fournisseur enregistrée", f.MontantRetenue > 0 ? $"Retenue à la source : {f.MontantRetenue:N3} TND" : "Aucune retenue à la source"], Source: "Backend", ClientId: f.FournisseurId.ToString());

    private static DocumentFluxDto FromPaiementFournisseur(PaiementFournisseur p, List<FactureFournisseur> factures, Dictionary<Guid, string> fournisseurs)
    {
        var facture = factures.FirstOrDefault(f => f.Id == p.FactureFournisseurId);
        return new($"paiement-fournisseur:{p.Id}", "paiement_emis", $"PAY-F-{p.DatePaiement:yyyy}-{p.Id.ToString("N")[..6].ToUpperInvariant()}",
            facture is null ? "Fournisseur" : fournisseurs.GetValueOrDefault(facture.FournisseurId, "Fournisseur"), p.Montant, "Payée", p.DatePaiement.ToString("yyyy-MM-dd"), null,
            facture?.Numero, null, [$"Paiement fournisseur {p.Mode}", $"Facture liée : {facture?.Numero ?? "-"}"], "Backend");
    }

    private static string MapFactureStatus(string statut, bool enRetard)
    {
        if (enRetard) return "Retard";
        return statut switch
        {
            "Brouillon" => "Brouillon",
            "Validee" or "Conforme" => "Émise",
            "Transmise" or "Acceptee" or "PartiellemementPayee" => "Envoyé",
            "Payee" => "Payée",
            "Rejetee" => "Refusé",
            "Annulee" => "Annulé",
            _ => "En cours"
        };
    }

    private static string MapAvoirStatus(StatutFacture statut)
        => statut switch
        {
            StatutFacture.Brouillon => "Brouillon",
            StatutFacture.Payee or StatutFacture.Acceptee => "Appliqué",
            StatutFacture.Annulee or StatutFacture.Rejetee => "Annulé",
            _ => "Émis"
        };

    private static string MapDocumentType(string? documentType, TypeTransaction transactionType)
    {
        var value = (documentType ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("é", "e")
            .Replace("è", "e")
            .Replace("ê", "e")
            .Replace("à", "a")
            .Replace("'", " ");

        if (value.Contains("commande")) return "bon_commande";
        if (value.Contains("reception")) return "bon_reception";
        if (value.Contains("livraison")) return "bon_livraison";
        if (value.Contains("prestation")) return "prestation";
        if (value.Contains("sortie")) return "bon_sortie";
        if (value.Contains("fabrication")) return "ordre_fabrication";
        if (value.Contains("proforma")) return "proforma";
        if (value.Contains("avoir")) return "avoir";
        if (value.Contains("paiement") && transactionType == TypeTransaction.Sortie) return "paiement_emis";
        if (value.Contains("paiement")) return "recu_paiement";
        return transactionType == TypeTransaction.Sortie ? "paiement_emis" : "facture";
    }
}
