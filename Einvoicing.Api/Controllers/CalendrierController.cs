using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/calendrier")]
[Produces("application/json")]
[Authorize]
public sealed class CalendrierController(
    ICalendrierService service,
    ICurrentUserService currentUser
) : ControllerBase
{
    private IActionResult? EntrepriseRequise(out Guid entrepriseId)
    {
        entrepriseId = Guid.Empty;
        if (currentUser.EntrepriseId is null)
            return Problem(title: "Entreprise non rattachee", statusCode: 403);
        entrepriseId = currentUser.EntrepriseId.Value;
        return null;
    }

    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendrierEventDto>), 200)]
    public async Task<IActionResult> ListerEvents(CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        return Ok(await service.ListerEventsAsync(entrepriseId, ct));
    }

    [HttpPost("events")]
    [ProducesResponseType(typeof(CalendrierEventDto), 201)]
    public async Task<IActionResult> CreerEvent([FromBody] CalendrierEventRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        var result = await service.CreerEventAsync(entrepriseId, request, ct);
        return CreatedAtAction(nameof(ListerEvents), result);
    }

    [HttpPut("events/{id:guid}")]
    [ProducesResponseType(typeof(CalendrierEventDto), 200)]
    public async Task<IActionResult> MettreAJourEvent(Guid id, [FromBody] CalendrierEventRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        return Ok(await service.MettreAJourEventAsync(id, entrepriseId, request, ct));
    }

    [HttpDelete("events/{id:guid}")]
    public async Task<IActionResult> SupprimerEvent(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        await service.SupprimerEventAsync(id, entrepriseId, ct);
        return NoContent();
    }

    [HttpGet("tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendrierTaskDto>), 200)]
    public async Task<IActionResult> ListerTasks(CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        return Ok(await service.ListerTasksAsync(entrepriseId, ct));
    }

    [HttpPost("tasks")]
    [ProducesResponseType(typeof(CalendrierTaskDto), 201)]
    public async Task<IActionResult> CreerTask([FromBody] CalendrierTaskRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        var result = await service.CreerTaskAsync(entrepriseId, request, ct);
        return CreatedAtAction(nameof(ListerTasks), result);
    }

    [HttpPatch("tasks/{id:guid}")]
    [ProducesResponseType(typeof(CalendrierTaskDto), 200)]
    public async Task<IActionResult> BasculerTask(Guid id, [FromBody] ToggleCalendrierTaskRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        return Ok(await service.BasculerTaskAsync(id, entrepriseId, request, ct));
    }

    [HttpDelete("tasks/{id:guid}")]
    public async Task<IActionResult> SupprimerTask(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId);
        if (guard is not null) return guard;
        await service.SupprimerTaskAsync(id, entrepriseId, ct);
        return NoContent();
    }
}
