using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/paiements")]
[Produces("application/json")]
[Authorize]
public sealed class PaiementsController(
    IPaiementService paiementService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private Guid EntrepriseId => currentUser.EntrepriseId!.Value;
    private Guid UtilisateurId => currentUser.UtilisateurId!.Value;

    [HttpPost]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(PaiementDto), 201)]
    public async Task<IActionResult> Enregistrer(
        [FromBody] EnregistrerPaiementRequest req, CancellationToken ct)
    {
        var result = await paiementService.EnregistrerAsync(EntrepriseId, UtilisateurId, req, ct);
        return CreatedAtAction(nameof(ListerParFacture), new { factureId = result.FactureId }, result);
    }

    [HttpGet("facture/{factureId:guid}")]
    [ProducesResponseType(typeof(ListePaiementsDto), 200)]
    public async Task<IActionResult> ListerParFacture(Guid factureId, CancellationToken ct)
    {
        var result = await paiementService.ListerParFactureAsync(factureId, EntrepriseId, ct);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListePaiementsDto), 200)]
    public async Task<IActionResult> ListerTous(
        [FromQuery] int page = 1,
        [FromQuery] int parPage = 20,
        CancellationToken ct = default)
    {
        var result = await paiementService.ListerParEntrepriseAsync(EntrepriseId, page, parPage, ct);
        return Ok(result);
    }
}
