using System.Text.Json;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/factures-fournisseur")]
[Authorize]
public sealed class FacturesFournisseurController(ContextBaseDeDonnees db, ICurrentUserService currentUser) : ControllerBase
{
    public record LigneFactureFournisseur(string Designation, decimal Quantite, decimal PrixUnitaire, decimal TauxTva, string Unite = "U", string? Description = null, decimal TauxRemise = 0);
    public record CreerRequete(string Numero, string Fournisseur, string? MatriculeFiscal, DateTime DateFacture, DateTime DateEcheance, List<LigneFactureFournisseur> Lignes, decimal TauxRetenue = 0, string? Reference = null, string? Notes = null, Guid? TransactionOrigineId = null);
    public record PaiementRequete(decimal Montant, ModePaiement Mode, DateTime DatePaiement, string? Reference = null, string? Notes = null);

    [HttpGet]
    public async Task<IActionResult> Lister(CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var fournisseurs = await db.Fournisseurs.Where(f => f.EntrepriseId == entrepriseId).ToDictionaryAsync(f => f.Id, f => f.Nom, ct);
        var rows = await db.FacturesFournisseur.Where(f => f.EntrepriseId == entrepriseId).OrderByDescending(f => f.DateFacture).ToListAsync(ct);
        return Ok(rows.Select(f => Dto(f, fournisseurs.GetValueOrDefault(f.FournisseurId, "Fournisseur"))));
    }

    [HttpPost]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Creer(CreerRequete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        if (string.IsNullOrWhiteSpace(r.Fournisseur)) return BadRequest(new { message = "Le fournisseur est requis." });
        var fournisseur = await db.Fournisseurs.FirstOrDefaultAsync(f => f.EntrepriseId == entrepriseId && (f.MatriculeFiscal == r.MatriculeFiscal || f.Nom == r.Fournisseur), ct);
        if (fournisseur is null) { fournisseur = Fournisseur.Creer(entrepriseId, r.Fournisseur, r.MatriculeFiscal); db.Fournisseurs.Add(fournisseur); }
        var totals = Totals(r.Lignes);
        var facture = FactureFournisseur.Creer(entrepriseId, fournisseur.Id, userId, r.Numero, r.DateFacture, r.DateEcheance, totals.ht, totals.tva, r.TauxRetenue, JsonSerializer.Serialize(r.Lignes), r.Reference, r.Notes, r.TransactionOrigineId);
        db.FacturesFournisseur.Add(facture); await db.SaveChangesAsync(ct);
        return Created($"/api/factures-fournisseur/{facture.Id}", Dto(facture, fournisseur.Nom));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Modifier(Guid id, CreerRequete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var facture = await db.FacturesFournisseur.SingleOrDefaultAsync(f => f.Id == id && f.EntrepriseId == entrepriseId, ct); if (facture is null) return NotFound();
        var totals = Totals(r.Lignes);
        facture.MettreAJour(r.DateFacture, r.DateEcheance, totals.ht, totals.tva, r.TauxRetenue, JsonSerializer.Serialize(r.Lignes), r.Reference, r.Notes);
        await db.SaveChangesAsync(ct); return Ok(Dto(facture, await NomFournisseur(facture.FournisseurId, ct)));
    }

    [HttpPost("{id:guid}/valider")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Valider(Guid id, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var facture = await db.FacturesFournisseur.SingleOrDefaultAsync(f => f.Id == id && f.EntrepriseId == entrepriseId, ct); if (facture is null) return NotFound();
        facture.Valider(); await db.SaveChangesAsync(ct); return Ok(Dto(facture, await NomFournisseur(facture.FournisseurId, ct)));
    }

    [HttpPost("{id:guid}/paiements")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Payer(Guid id, PaiementRequete r, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out var userId, out var error)) return error!;
        var facture = await db.FacturesFournisseur.SingleOrDefaultAsync(f => f.Id == id && f.EntrepriseId == entrepriseId, ct); if (facture is null) return NotFound();
        facture.EnregistrerPaiement(r.Montant);
        var paiement = PaiementFournisseur.Creer(facture.Id, entrepriseId, userId, r.Montant, r.Mode, r.DatePaiement, r.Reference, r.Notes);
        db.PaiementsFournisseur.Add(paiement);
        db.Transactions.Add(Transaction.Creer(entrepriseId, userId, r.DatePaiement, $"Paiement fournisseur {facture.Numero}", r.Montant, TypeTransaction.Sortie, tiersNom: await NomFournisseur(facture.FournisseurId, ct), categorieNom: "Achats", description: $"Règlement de la facture fournisseur {facture.Numero}", source: DocumentSource.Web, statut: StatutTransaction.Justifiee));
        await db.SaveChangesAsync(ct);
        return Ok(new { facture = Dto(facture, await NomFournisseur(facture.FournisseurId, ct)), paiement });
    }

    [HttpGet("{id:guid}/paiements")]
    public async Task<IActionResult> ListerPaiements(Guid id, CancellationToken ct)
    {
        if (!Context(out var entrepriseId, out _, out var error)) return error!;
        var exists = await db.FacturesFournisseur.AnyAsync(f => f.Id == id && f.EntrepriseId == entrepriseId, ct); if (!exists) return NotFound();
        return Ok(await db.PaiementsFournisseur.Where(p => p.FactureFournisseurId == id).OrderByDescending(p => p.DatePaiement).ToListAsync(ct));
    }

    private bool Context(out Guid entrepriseId, out Guid userId, out IActionResult? error) { entrepriseId = currentUser.EntrepriseId ?? Guid.Empty; userId = currentUser.UtilisateurId ?? Guid.Empty; error = entrepriseId == Guid.Empty ? Forbid() : userId == Guid.Empty ? Unauthorized() : null; return error is null; }
    private async Task<string> NomFournisseur(Guid id, CancellationToken ct) => await db.Fournisseurs.Where(f => f.Id == id).Select(f => f.Nom).FirstOrDefaultAsync(ct) ?? "Fournisseur";
    private static (decimal ht, decimal tva) Totals(IEnumerable<LigneFactureFournisseur> lignes) { var ht = lignes.Sum(l => Math.Round(l.Quantite * l.PrixUnitaire * (1 - l.TauxRemise / 100m), 3)); var tva = lignes.Sum(l => Math.Round(l.Quantite * l.PrixUnitaire * (1 - l.TauxRemise / 100m) * l.TauxTva / 100m, 3)); return (ht, tva); }
    private static object Dto(FactureFournisseur f, string fournisseur) => new { f.Id, f.Numero, fournisseur, f.FournisseurId, f.Statut, f.DateFacture, f.DateEcheance, f.TotalHt, f.TotalTva, f.TotalTtc, f.TauxRetenue, f.MontantRetenue, f.NetAPayer, f.MontantPaye, montantRestant = f.MontantRestant, f.Reference, f.Notes, f.TransactionOrigineId };
}
