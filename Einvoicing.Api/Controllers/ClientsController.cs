




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Produces("application/json")]
[Authorize]
public sealed class ClientsController(
    IClientService clientService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private IActionResult? EntrepriseRequise(out Guid entrepriseId)
    {
        entrepriseId = Guid.Empty;
        if (currentUser.EntrepriseId is null)
            return Problem(
                title: "Entreprise non rattachée",
                detail: "Votre compte n'est pas encore rattaché à une entreprise. Reconnectez-vous.",
                statusCode: 403);
        entrepriseId = currentUser.EntrepriseId.Value;
        return null;
    }

    [HttpPost]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(ClientDto), 201)]
    public async Task<IActionResult> Creer(
        [FromBody] CreerClientRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await clientService.CreerAsync(eId, req, ct);
        return CreatedAtAction(nameof(ObtenirParId), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListeClientsDto), 200)]
    public async Task<IActionResult> Lister(
        [FromQuery] int page = 1,
        [FromQuery] int parPage = 20,
        [FromQuery] bool? actifSeulement = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await clientService.ListerAsync(eId, page, parPage, actifSeulement, ct);
        return Ok(result);
    }

    [HttpGet("rechercher")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientDto>), 200)]
    public async Task<IActionResult> Rechercher(
        [FromQuery] string terme, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await clientService.RechercherAsync(eId, terme, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientDto), 200)]
    public async Task<IActionResult> ObtenirParId(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await clientService.ObtenirParIdAsync(id, eId, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(ClientDto), 200)]
    public async Task<IActionResult> MettreAJour(
        Guid id, [FromBody] MettreAJourClientRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await clientService.MettreAJourAsync(id, eId, req, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/desactiver")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Desactiver(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        await clientService.DesactiverAsync(id, eId, ct);
        return Ok(new { message = "Client désactivé." });
    }

    [HttpPost("{id:guid}/reactiver")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> Reactiver(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        await clientService.ReactiverAsync(id, eId, ct);
        return Ok(new { message = "Client réactivé." });
    }
}
