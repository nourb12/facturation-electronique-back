using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/parametres-fiscaux")]
[Produces("application/json")]
[Authorize]
public sealed class ParametresFiscauxController(
    IParametreFiscalService service,
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
    [ProducesResponseType(typeof(IReadOnlyList<ParametreFiscalDto>), 200)]
    public async Task<IActionResult> Lister(CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await service.ListerAsync(eId, ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ParametreFiscalDto), 201)]
    public async Task<IActionResult> Creer([FromBody] CreerParametreFiscalRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await service.CreerAsync(eId, req, ct);
        return CreatedAtAction(nameof(Lister), result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ParametreFiscalDto), 200)]
    public async Task<IActionResult> MettreAJour(
        Guid id, [FromBody] MettreAJourParametreFiscalRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await service.MettreAJourAsync(id, eId, req, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Supprimer(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        await service.SupprimerAsync(id, eId, ct);
        return NoContent();
    }
}