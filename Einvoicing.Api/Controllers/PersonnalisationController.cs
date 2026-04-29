using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/personnalisation")]
[Produces("application/json")]
[Authorize]
public sealed class PersonnalisationController(
    IPersonnalisationService service,
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

    [HttpGet]
    [ProducesResponseType(typeof(PersonnalisationDto), 200)]
    public async Task<IActionResult> Obtenir(CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await service.ObtenirAsync(eId, ct);
        return Ok(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(PersonnalisationDto), 200)]
    public async Task<IActionResult> Enregistrer([FromBody] EnregistrerPersonnalisationRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await service.EnregistrerAsync(eId, req, ct);
        return Ok(result);
    }
}