using System.Text.Json;
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
[Route("api/documents-commerciaux")]
[Authorize]
public sealed class DocumentsCommerciauxController(ContextBaseDeDonnees db, ICurrentUserService currentUser, IFactureService factureService) : ControllerBase
{
    public record Ligne(string Designation, decimal Quantite, decimal PrixUnitaire, decimal TauxTva, string Unite = "U", string? Description = null, decimal TauxRemise = 0);
    public record Requete(string Type, Guid ClientId, string? Numero, DateTime Date, DateTime? Echeance, string Statut, List<Ligne> Lignes, string? Reference = null, string? Notes = null, Guid? OrigineId = null);
    public record ConversionLivraisonRequete(string? Numero = null, DateTime? Date = null, DateTime? Echeance = null);
    public record StatutRequete(string Statut);

    [HttpGet]
    public async Task<IActionResult> Lister(CancellationToken ct)
    {
        if (!Context(out _, out _, out var error)) return error!;
        return Ok(await Query().OrderByDescending(x => x.DateDocument).ToListAsync(ct));
    }

    [HttpPost]
    [Authorize(Policy = "Ventes")]
    public async Task<IActionResult> Creer(Requete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        var client = await db.Clients.AnyAsync(c => c.Id == r.ClientId && c.EntrepriseId == entrepriseId && c.EstActif, ct);
        if (!client) return BadRequest(new { message = "Client invalide." });
        if (r.OrigineId.HasValue && !await Query().AnyAsync(d => d.Id == r.OrigineId && d.ClientId == r.ClientId, ct))
            return BadRequest(new { message = "Document d'origine invalide." });
        if (r.Lignes is null || r.Lignes.Count == 0) return BadRequest(new { message = "Le document doit contenir au moins une ligne." });
        var totals = Totals(r.Lignes);
        // Numerotation continue attribuee par le serveur : le numero envoye par le client est ignore.
        var lignesJson = JsonSerializer.Serialize(r.Lignes);
        var doc = await EnregistrerAvecNumeroAsync(entrepriseId, r.Type, r.Date, numero =>
            DocumentCommercial.Creer(entrepriseId, r.ClientId, userId, r.Type, numero, r.Date, r.Echeance, totals.ht, totals.tva, totals.ttc, lignesJson, r.Reference, r.Notes, r.OrigineId), ct);
        return Created($"/api/documents-commerciaux/{doc.Id}", doc);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Ventes")]
    public async Task<IActionResult> Modifier(Guid id, Requete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var doc = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct); if (doc is null) return NotFound();
        var totals = Totals(r.Lignes); doc.MettreAJour(r.Statut, r.Echeance, JsonSerializer.Serialize(r.Lignes), totals.ht, totals.tva, totals.ttc, r.Reference, r.Notes);
        await db.SaveChangesAsync(ct); return Ok(doc);
    }

    [HttpPatch("{id:guid}/statut")]
    [Authorize(Policy = "Ventes")]
    public async Task<IActionResult> ModifierStatut(Guid id, StatutRequete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var doc = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct);
        if (doc is null) return NotFound();
        doc.MettreAJour(r.Statut, doc.DateEcheance, doc.LignesJson, doc.TotalHt, doc.TotalTva, doc.TotalTtc, doc.Reference, doc.Notes);
        await db.SaveChangesAsync(ct);
        return Ok(doc);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Ventes")]
    public async Task<IActionResult> Supprimer(Guid id, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var doc = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct);
        if (doc is null) return NotFound();
        if (doc.FactureCreeeId.HasValue) return BadRequest(new { message = "Un document déjà facturé ne peut pas être supprimé." });
        db.DocumentsCommerciaux.Remove(doc);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/convertir-livraison")]
    [Authorize(Policy = "Ventes")]
    public async Task<IActionResult> ConvertirEnLivraison(Guid id, ConversionLivraisonRequete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        var commande = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct);
        if (commande is null) return NotFound();
        if (commande.Type != "bon_commande") return BadRequest(new { message = "Seul un bon de commande peut être livré." });
        if (await Query().AnyAsync(d => d.DocumentOrigineId == commande.Id && d.Type == "bon_livraison", ct))
            return Conflict(new { message = "Ce bon de commande a déjà été livré." });

        var dateLivraison = r.Date ?? DateTime.UtcNow;
        var livraison = await EnregistrerAvecNumeroAsync(entrepriseId, "bon_livraison", dateLivraison, numero =>
            DocumentCommercial.Creer(
                entrepriseId, commande.ClientId, userId, "bon_livraison", numero,
                dateLivraison, r.Echeance ?? commande.DateEcheance,
                commande.TotalHt, commande.TotalTva, commande.TotalTtc, commande.LignesJson,
                commande.Reference, commande.Notes, commande.Id), ct);
        return Created($"/api/documents-commerciaux/{livraison.Id}", livraison);
    }

    [HttpPost("{id:guid}/convertir-facture")]
    [Authorize(Policy = "Ventes")]
    public async Task<IActionResult> Convertir(Guid id, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        var doc = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct); if (doc is null) return NotFound();
        if (doc.Type != "bon_livraison") return BadRequest(new { message = "Seul un bon de livraison peut etre facture." });
        if (doc.FactureCreeeId.HasValue) return Conflict(new { message = "Ce document a deja ete facture." });
        var lignes = JsonSerializer.Deserialize<List<Ligne>>(doc.LignesJson) ?? [];
        var invoice = await factureService.CreerAsync(entrepriseId, userId, new CreerFactureRequest(doc.ClientId, TypeFacture.Facture, ModePaiement.Virement, doc.DateEcheance ?? DateTime.UtcNow.AddDays(30), lignes.Select(l => new CreerLigneFactureRequest(l.Designation,l.Quantite,l.PrixUnitaire,l.TauxTva,null,l.Description,l.Unite,l.TauxRemise)).ToList(), doc.Reference ?? doc.Numero, doc.Notes, null), ct);
        doc.MarquerConverti(invoice.Id); await db.SaveChangesAsync(ct); return Created($"/api/factures/{invoice.Id}", invoice);
    }

    /// <summary>
    /// Attribue le prochain numero et enregistre le document. L'index unique (EntrepriseId, Numero)
    /// garantit l'absence de doublon : si deux creations simultanees obtiennent le meme numero,
    /// la seconde est rejetee par la base et on recommence avec le numero suivant.
    /// </summary>
    private async Task<DocumentCommercial> EnregistrerAvecNumeroAsync(
        Guid entrepriseId, string type, DateTime date, Func<string, DocumentCommercial> creer, CancellationToken ct)
    {
        const int tentativesMax = 5;
        for (var tentative = 1; ; tentative++)
        {
            var doc = creer(await ProchainNumeroAsync(entrepriseId, type, date, ct));
            db.DocumentsCommerciaux.Add(doc);
            try
            {
                await db.SaveChangesAsync(ct);
                return doc;
            }
            catch (DbUpdateException ex) when (EstDoublonDeNumero(ex) && tentative < tentativesMax)
            {
                db.Entry(doc).State = EntityState.Detached;
            }
        }
    }

    internal static bool EstDoublonDeNumero(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };

    private async Task<string> ProchainNumeroAsync(Guid entrepriseId, string type, DateTime date, CancellationToken ct)
    {
        var prefixe = type switch { "bon_commande" => "BC", "bon_livraison" => "BL", _ => "DOC" };
        var racine = $"{prefixe}-{date.Year}-";
        var existants = await db.DocumentsCommerciaux
            .Where(d => d.EntrepriseId == entrepriseId && d.Type == type && d.Numero.StartsWith(racine))
            .Select(d => d.Numero)
            .ToListAsync(ct);
        var max = existants
            .Select(n => int.TryParse(n[racine.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"{racine}{max + 1:D4}";
    }

    private IQueryable<DocumentCommercial> Query() => db.DocumentsCommerciaux.Where(d => d.EntrepriseId == currentUser.EntrepriseId);
    private bool Context(out Guid entrepriseId, out Guid userId, out IActionResult? error) { entrepriseId = currentUser.EntrepriseId ?? Guid.Empty; userId = currentUser.UtilisateurId ?? Guid.Empty; error = entrepriseId == Guid.Empty ? Forbid() : userId == Guid.Empty ? Unauthorized() : null; return error is null; }
    private static (decimal ht, decimal tva, decimal ttc) Totals(IEnumerable<Ligne> lignes)
    {
        var validated = lignes.Select(l => LigneFacture.Creer(Guid.NewGuid(), 1, l.Designation,
            l.Quantite, l.PrixUnitaire, l.TauxTva, null, l.Description, l.Unite, l.TauxRemise)).ToList();
        var ht = validated.Sum(l => l.MontantHt);
        var tva = validated.Sum(l => l.MontantTva);
        return (ht, tva, ht + tva);
    }
}
