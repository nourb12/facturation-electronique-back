using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

public sealed record AuditTimelineItem(DateTime Date, string Utilisateur, string Action, string Ressource, string Ip, bool Succes, string? Details, string? Entreprise = null);

[ApiController]
[Route("api/admin/abonnements")]
[Produces("application/json")]
[Authorize(Policy = "SuperOuAdmin")]
public sealed class AdminAbonnementsController(ContextBaseDeDonnees db, IEmailService emailService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lister(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        var rows = await db.AbonnementsSaas
            .Join(db.Entreprises, a => a.EntrepriseId, e => e.Id, (a, e) => new { a, e })
            .Select(x => new
            {
                id = x.a.Id, entrepriseId = x.e.Id, entreprise = x.e.Nom, plan = x.a.Plan, statut = x.a.Statut,
                prixMensuel = x.a.PrixMensuel, prochainRenouvellement = x.a.ProchainRenouvellement,
                limiteUtilisateurs = x.a.LimiteUtilisateurs, limiteFactures = x.a.LimiteFacturesMensuelles,
                utilisateurs = db.Utilisateurs.Count(u => u.EntrepriseId == x.e.Id),
                factures = db.Factures.Count(f => f.EntrepriseId == x.e.Id && f.DateEmission >= monthStart && f.DateEmission < nextMonth)
            })
            .OrderBy(x => x.entreprise)
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("entreprises-disponibles")]
    public async Task<IActionResult> EntreprisesDisponibles(CancellationToken ct)
    {
        var idsAbonnes = db.AbonnementsSaas.Select(a => a.EntrepriseId);
        var rows = await db.Entreprises.Where(e => e.EstActive && !idsAbonnes.Contains(e.Id))
            .OrderBy(e => e.Nom).Select(e => new { id = e.Id, nom = e.Nom }).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Creer([FromBody] CreerAbonnementRequest req, CancellationToken ct)
    {
        var entreprise = await db.Entreprises.FindAsync([req.EntrepriseId], ct);
        if (entreprise is null) return NotFound(new { message = "Entreprise introuvable." });
        if (await db.AbonnementsSaas.AnyAsync(a => a.EntrepriseId == req.EntrepriseId, ct))
            return Conflict(new { message = "Cette entreprise possède déjà un abonnement." });

        var abonnement = AbonnementSaas.Creer(req.EntrepriseId, req.Plan, req.PrixMensuel, req.LimiteUtilisateurs, req.LimiteFacturesMensuelles, req.ProchainRenouvellement);
        db.AbonnementsSaas.Add(abonnement);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Lister), new { abonnement.Id });
    }

    [HttpPatch("{id:guid}/statut")]
    public async Task<IActionResult> ChangerStatut(Guid id, [FromBody] ChangerStatutAbonnementRequest req, CancellationToken ct)
    {
        var abonnement = await db.AbonnementsSaas.FindAsync([id], ct);
        if (abonnement is null) return NotFound();
        abonnement.ChangerStatut(req.Statut);
        await db.SaveChangesAsync(ct);
        return Ok(new { abonnement.Id, abonnement.Statut });
    }

    [HttpPost("{id:guid}/relancer")]
    public async Task<IActionResult> Relancer(Guid id, CancellationToken ct)
    {
        var row = await db.AbonnementsSaas.Join(db.Entreprises, a => a.EntrepriseId, e => e.Id, (a, e) => new { a, e })
            .FirstOrDefaultAsync(x => x.a.Id == id, ct);
        if (row is null) return NotFound();

        await emailService.EnvoyerNotifFactureAsync(row.e.Email, row.e.Nom, $"ABO-{row.a.Id.ToString()[..8]}", "rappel", row.e.Telephone ?? string.Empty, row.a.PrixMensuel, ct);
        return Ok(new { message = "Relance d'abonnement envoyée par e-mail." });
    }
}

public sealed record CreerAbonnementRequest(Guid EntrepriseId, string Plan, decimal PrixMensuel, int LimiteUtilisateurs, int LimiteFacturesMensuelles, DateTime? ProchainRenouvellement);
public sealed record ChangerStatutAbonnementRequest(string Statut);
