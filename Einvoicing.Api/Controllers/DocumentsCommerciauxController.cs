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
    public record Requete(string Type, Guid ClientId, string Numero, DateTime Date, DateTime? Echeance, string Statut, List<Ligne> Lignes, string? Reference = null, string? Notes = null, Guid? OrigineId = null);
    public record ConversionLivraisonRequete(string Numero, DateTime? Date = null, DateTime? Echeance = null);
    public record StatutRequete(string Statut);

    [HttpGet]
    public async Task<IActionResult> Lister(CancellationToken ct) => Ok(await Query().OrderByDescending(x => x.DateDocument).ToListAsync(ct));

    [HttpPost]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Creer(Requete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        var client = await db.Clients.AnyAsync(c => c.Id == r.ClientId && c.EntrepriseId == entrepriseId && c.EstActif, ct);
        if (!client) return BadRequest(new { message = "Client invalide." });
        var totals = Totals(r.Lignes);
        var doc = DocumentCommercial.Creer(entrepriseId, r.ClientId, userId, r.Type, r.Numero, r.Date, r.Echeance, totals.ht, totals.tva, totals.ttc, JsonSerializer.Serialize(r.Lignes), r.Reference, r.Notes, r.OrigineId);
        db.DocumentsCommerciaux.Add(doc); await db.SaveChangesAsync(ct); return Created($"/api/documents-commerciaux/{doc.Id}", doc);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Modifier(Guid id, Requete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var doc = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct); if (doc is null) return NotFound();
        var totals = Totals(r.Lignes); doc.MettreAJour(r.Statut, r.Echeance, JsonSerializer.Serialize(r.Lignes), totals.ht, totals.tva, totals.ttc, r.Reference, r.Notes);
        await db.SaveChangesAsync(ct); return Ok(doc);
    }

    [HttpPatch("{id:guid}/statut")]
    [Authorize(Policy = "Tous")]
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
    [Authorize(Policy = "Tous")]
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
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> ConvertirEnLivraison(Guid id, ConversionLivraisonRequete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        var commande = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct);
        if (commande is null) return NotFound();
        if (commande.Type != "bon_commande") return BadRequest(new { message = "Seul un bon de commande peut être livré." });
        if (string.IsNullOrWhiteSpace(r.Numero)) return BadRequest(new { message = "Le numéro du bon de livraison est requis." });

        var livraison = DocumentCommercial.Creer(
            entrepriseId, commande.ClientId, userId, "bon_livraison", r.Numero,
            r.Date ?? DateTime.UtcNow, r.Echeance ?? commande.DateEcheance,
            commande.TotalHt, commande.TotalTva, commande.TotalTtc, commande.LignesJson,
            commande.Reference, commande.Notes, commande.Id);
        db.DocumentsCommerciaux.Add(livraison);
        await db.SaveChangesAsync(ct);
        return Created($"/api/documents-commerciaux/{livraison.Id}", livraison);
    }

    [HttpPost("{id:guid}/convertir-facture")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Convertir(Guid id, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        var doc = await Query().SingleOrDefaultAsync(x => x.Id == id && x.EntrepriseId == entrepriseId, ct); if (doc is null) return NotFound();
        if (doc.Type != "bon_livraison") return BadRequest(new { message = "Seul un bon de livraison peut etre facture." });
        var lignes = JsonSerializer.Deserialize<List<Ligne>>(doc.LignesJson) ?? [];
        var invoice = await factureService.CreerAsync(entrepriseId, userId, new CreerFactureRequest(doc.ClientId, TypeFacture.Facture, ModePaiement.Virement, doc.DateEcheance ?? DateTime.UtcNow.AddDays(30), lignes.Select(l => new CreerLigneFactureRequest(l.Designation,l.Quantite,l.PrixUnitaire,l.TauxTva,null,l.Description,l.Unite,l.TauxRemise)).ToList(), doc.Reference ?? doc.Numero, doc.Notes, null), ct);
        doc.MarquerConverti(invoice.Id); await db.SaveChangesAsync(ct); return Created($"/api/factures/{invoice.Id}", invoice);
    }

    private IQueryable<DocumentCommercial> Query() => db.DocumentsCommerciaux.AsQueryable();
    private bool Context(out Guid entrepriseId, out Guid userId, out IActionResult? error) { entrepriseId = currentUser.EntrepriseId ?? Guid.Empty; userId = currentUser.UtilisateurId ?? Guid.Empty; error = entrepriseId == Guid.Empty ? Forbid() : userId == Guid.Empty ? Unauthorized() : null; return error is null; }
    private static (decimal ht, decimal tva, decimal ttc) Totals(IEnumerable<Ligne> lignes) { var ht = lignes.Sum(l => Math.Round(l.Quantite*l.PrixUnitaire*(1-l.TauxRemise/100m),3)); var tva = lignes.Sum(l => Math.Round(l.Quantite*l.PrixUnitaire*(1-l.TauxRemise/100m)*l.TauxTva/100m,3)); return (ht,tva,ht+tva); }
}
