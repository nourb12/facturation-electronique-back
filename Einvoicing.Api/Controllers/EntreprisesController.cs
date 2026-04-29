




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/entreprises")]
[Produces("application/json")]
[Authorize]
public sealed class EntreprisesController(
    IEntrepriseService entrepriseService,
    ICurrentUserService currentUser
) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "SuperOuAdmin")]
    [ProducesResponseType(typeof(EntrepriseDto), 201)]
    public async Task<IActionResult> Creer(
        [FromBody] CreerEntrepriseRequest req, CancellationToken ct)
    {
        if (currentUser.UtilisateurId is null) return Unauthorized();
        var result = await entrepriseService.CreerAsync(req, currentUser.UtilisateurId.Value, ct);
        return CreatedAtAction(nameof(ObtenirParId), new { id = result.Id }, result);
    }

    [HttpGet]
    [Authorize(Policy = "SuperAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<EntrepriseDto>), 200)]
    public async Task<IActionResult> ListerToutes(CancellationToken ct)
    {
        var result = await entrepriseService.ListerToutesAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EntrepriseDto), 200)]
    public async Task<IActionResult> ObtenirParId(Guid id, CancellationToken ct)
    {
        var result = await entrepriseService.ObtenirParIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ResponsableOuAdmin")]
    [ProducesResponseType(typeof(EntrepriseDto), 200)]
    public async Task<IActionResult> MettreAJour(
        Guid id, [FromBody] MettreAJourEntrepriseRequest req, CancellationToken ct)
    {
        var result = await entrepriseService.MettreAJourAsync(id, req, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/teif")]
    [Authorize(Policy = "ResponsableOuAdmin")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ConfigurerTeif(
        Guid id, [FromBody] ConfigurerTeifRequest req, CancellationToken ct)
    {
        await entrepriseService.ConfigurerTeifAsync(id, req, ct);
        return Ok(new { message = "Paramètres TEIF mis à jour." });
    }

    [HttpPost("{id:guid}/desactiver")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> Desactiver(Guid id, CancellationToken ct)
    {
        await entrepriseService.DesactiverAsync(id, ct);
        return Ok(new { message = "Entreprise désactivée." });
    }

    [HttpPost("{id:guid}/reactiver")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> Reactiver(Guid id, CancellationToken ct)
    {
        await entrepriseService.ReactiverAsync(id, ct);
        return Ok(new { message = "Entreprise réactivée." });
    }
}
