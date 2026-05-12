using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/fournisseurs")]
[Produces("application/json")]
[Authorize]
public sealed class FournisseursController(
    IFournisseurService fournisseurService,
    ICurrentUserService currentUser) : ControllerBase
{
    private IActionResult? EntrepriseRequise(out Guid entrepriseId)
    {
        entrepriseId = Guid.Empty;
        if (currentUser.EntrepriseId is null)
            return Problem(
                title: "Entreprise non rattachée",
                detail: "Votre compte n'est pas encore rattaché à une entreprise.",
                statusCode: 403);

        entrepriseId = currentUser.EntrepriseId.Value;
        return null;
    }

    [HttpPost("match-or-create")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FournisseurMatchResultDto), 200)]
    public async Task<IActionResult> MatchOrCreate([FromBody] MatchOrCreateFournisseurRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;

        var result = await fournisseurService.MatchOrCreateAsync(entrepriseId, request, ct);
        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(IReadOnlyList<FournisseurDto>), 200)]
    public async Task<IActionResult> Search([FromQuery] string term, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;

        var result = await fournisseurService.RechercherAsync(entrepriseId, term, ct);
        return Ok(result);
    }
}
