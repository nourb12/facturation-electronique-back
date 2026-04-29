using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/signatures")]
[Produces("application/json")]
[Authorize]
public sealed class SignaturesController(
    ISignatureService signatureService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private Guid EntrepriseId => currentUser.EntrepriseId!.Value;
    private Guid UtilisateurId => currentUser.UtilisateurId!.Value;

    [HttpPost("demander")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(SignatureDto), 200)]
    public async Task<IActionResult> Demander(
        [FromBody] DemanderSignatureRequest req, CancellationToken ct)
    {
        var result = await signatureService.DemanderSignatureAsync(
            req.FactureId, EntrepriseId, UtilisateurId, ct);
        return Ok(result);
    }

    [HttpGet("facture/{factureId:guid}")]
    [ProducesResponseType(typeof(SignatureDto), 200)]
    public async Task<IActionResult> ObtenirStatut(Guid factureId, CancellationToken ct)
    {
        var result = await signatureService.ObtenirStatutAsync(factureId, EntrepriseId, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/relancer")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(SignatureDto), 200)]
    public async Task<IActionResult> Relancer(Guid id, CancellationToken ct)
    {
        var result = await signatureService.RelancerAsync(id, EntrepriseId, ct);
        return Ok(result);
    }
}
