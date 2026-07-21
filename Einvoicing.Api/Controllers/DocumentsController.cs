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
        string Source);

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var entrepriseId = await EntrepriseIdAsync(ct);
        if (entrepriseId is null) return NotFound();

        var clients = await db.Clients
            .Where(c => c.EntrepriseId == entrepriseId)
            .ToDictionaryAsync(c => c.Id, c => c.Nom, ct);

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

        var factureById = factures.ToDictionary(f => f.Id, f => f);
        var rows = new List<DocumentFluxDto>();

        rows.AddRange(factures.Select(f => FromFacture(f, clients)));
        rows.AddRange(paiements.Select(p => FromPaiement(p, factureById, clients)));
        rows.AddRange(transactions.Where(t => t.FactureId is null).Select(FromTransaction));
        rows.AddRange(scans.Where(s => s.TransactionId is null).Select(FromScan));

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
    [Authorize(Policy = "Tous")]
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
            return BadRequest(new { message = "Seules les factures proforma et avoirs backend peuvent etre convertis automatiquement." });

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

    private async Task<Guid?> EntrepriseIdAsync(CancellationToken ct)
        => currentUser.EntrepriseId
           ?? await db.Entreprises.OrderByDescending(e => e.ModifieLe).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(ct);

    private static DocumentFluxDto FromFacture(Facture f, Dictionary<Guid, string> clients)
    {
        var type = f.TypeFacture switch
        {
            TypeFacture.Avoir => "avoir",
            TypeFacture.Proforma => "devis",
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
        var numero = t.JustificatifNomFichier ?? $"{(t.Type == TypeTransaction.Sortie ? "PAY-P" : "DOC")}-{t.Date:yyyy}-{t.Id.ToString("N")[..6].ToUpperInvariant()}";

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
        if (value.Contains("livraison")) return "bon_livraison";
        if (value.Contains("sortie")) return "bon_sortie";
        if (value.Contains("fabrication")) return "ordre_fabrication";
        if (value.Contains("proforma")) return "proforma";
        if (value.Contains("avoir")) return "avoir";
        if (value.Contains("paiement") && transactionType == TypeTransaction.Sortie) return "paiement_emis";
        if (value.Contains("paiement")) return "recu_paiement";
        return transactionType == TypeTransaction.Sortie ? "paiement_emis" : "facture";
    }
}
