




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/utilisateurs")]
[Produces("application/json")]
[Authorize(Policy = "ResponsableOuAdmin")]
public sealed class UtilisateursController(
    IUtilisateurService utilisateurService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private IActionResult? EntrepriseRequise(out Guid entrepriseId)
    {
        entrepriseId = Guid.Empty;
        if (currentUser.EntrepriseId is null)
            return Problem(title: "Entreprise non rattachée", statusCode: 403);
        entrepriseId = currentUser.EntrepriseId.Value;
        return null;
    }

    [HttpPost]
    [ProducesResponseType(typeof(UtilisateurListeDto), 201)]
    public async Task<IActionResult> Creer([FromBody] CreerUtilisateurRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await utilisateurService.CreerAsync(eId, req, ct);
        return CreatedAtAction(nameof(ObtenirParId), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UtilisateurListeDto>), 200)]
    public async Task<IActionResult> Lister(CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await utilisateurService.ListerAsync(eId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UtilisateurListeDto), 200)]
    public async Task<IActionResult> ObtenirParId(Guid id, CancellationToken ct)
    {
        var result = await utilisateurService.ObtenirParIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UtilisateurListeDto), 200)]
    public async Task<IActionResult> MettreAJour(
        Guid id, [FromBody] MettreAJourUtilisateurRequest req, CancellationToken ct)
    {
        var result = await utilisateurService.MettreAJourAsync(id, req, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/suspendre")]
    [Authorize(Policy = "SuperOuAdmin")]
    public async Task<IActionResult> Suspendre(Guid id, CancellationToken ct)
    {
        await utilisateurService.SuspendreAsync(id, ct);
        return Ok(new { message = "Utilisateur suspendu." });
    }

    [HttpPost("{id:guid}/reactiver")]
    [Authorize(Policy = "SuperOuAdmin")]
    public async Task<IActionResult> Reactiver(Guid id, CancellationToken ct)
    {
        await utilisateurService.ReactiverAsync(id, ct);
        return Ok(new { message = "Utilisateur réactivé." });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SuperOuAdmin")]
    public async Task<IActionResult> Supprimer(Guid id, CancellationToken ct)
    {
        await utilisateurService.SupprimerAsync(id, ct);
        return Ok(new { message = "Utilisateur supprimé." });
    }

    [HttpPost("{id:guid}/changer-mot-de-passe")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ChangerMotDePasse(
        Guid id, [FromBody] ChangerMotDePasseRequest req, CancellationToken ct)
    {
        if (currentUser.UtilisateurId != id)
            return Problem(title: "Accès refusé", statusCode: 403);

        await utilisateurService.ChangerMotDePasseAsync(id, req, ct);
        return Ok(new { message = "Mot de passe mis à jour." });
    }

    [HttpGet("{id:guid}/sessions")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionActiveDto>), 200)]
    public async Task<IActionResult> ObtenirSessions(Guid id, CancellationToken ct)
    {
        if (currentUser.UtilisateurId != id)
            return Problem(title: "Accès refusé", statusCode: 403);

        var result = await utilisateurService.ObtenirSessionsAsync(id, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/sessions/{sessionId:guid}")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> RevoquerSession(
        Guid id, Guid sessionId, CancellationToken ct)
    {
        if (currentUser.UtilisateurId != id)
            return Problem(title: "Accès refusé", statusCode: 403);

        await utilisateurService.RevoquerSessionAsync(id, sessionId, ct);
        return Ok(new { message = "Session révoquée." });
    }

    [HttpDelete("{id:guid}/sessions")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> RevoquerToutesSessions(Guid id, CancellationToken ct)
    {
        if (currentUser.UtilisateurId != id)
            return Problem(title: "Accès refusé", statusCode: 403);

        await utilisateurService.RevoquerToutesSessionsAsync(id, ct);
        return Ok(new { message = "Toutes les sessions ont été révoquées." });
    }
}
