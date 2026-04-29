using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/ttn")]
[Produces("application/json")]
[Authorize]
public sealed class EchangesTtnController(
    IEchangeTtnService echangeService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private Guid EntrepriseId => currentUser.EntrepriseId!.Value;
    private Guid UtilisateurId => currentUser.UtilisateurId!.Value;

    [HttpPost("envoyer")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(EchangeDto), 200)]
    public async Task<IActionResult> Envoyer(
        [FromBody] EnvoyerTtnRequest req, CancellationToken ct)
    {
        var result = await echangeService.EnvoyerAsync(req.FactureId, EntrepriseId, UtilisateurId, ct);
        return Ok(result);
    }

    [HttpPost("{echangeId:guid}/simuler/{scenario}")]
    [Authorize(Policy = "SuperOuAdmin")]
    [ProducesResponseType(typeof(EchangeDto), 200)]
    public async Task<IActionResult> SimulerReponse(
        Guid echangeId, string scenario, CancellationToken ct)
    {
        var result = await echangeService.SimulerReponseAsync(echangeId, scenario, ct);
        return Ok(result);
    }

    [HttpGet("facture/{factureId:guid}")]
    [ProducesResponseType(typeof(EchangeDto), 200)]
    public async Task<IActionResult> ObtenirStatut(Guid factureId, CancellationToken ct)
    {
        var result = await echangeService.ObtenirStatutAsync(factureId, EntrepriseId, ct);
        return Ok(result);
    }

    [HttpPost("{echangeId:guid}/relancer")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(EchangeDto), 200)]
    public async Task<IActionResult> Relancer(Guid echangeId, CancellationToken ct)
    {
        var result = await echangeService.RelancerAsync(echangeId, EntrepriseId, ct);
        return Ok(result);
    }
}
