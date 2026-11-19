using Einvoicing.Application.Configuration;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/admin/configuration")]
[Produces("application/json")]
[Authorize(Policy = "SuperAdmin")]
public sealed class AdminConfigurationController(
    IPlatformConfigurationService configuration,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlatformConfigurationDto>>> List(CancellationToken ct)
        => Ok(await configuration.ListAsync(ct));

    [HttpGet("{key}/history")]
    public async Task<ActionResult<IReadOnlyList<PlatformConfigurationHistoryDto>>> History(
        string key,
        CancellationToken ct)
        => Ok(await configuration.GetHistoryAsync(key, ct));

    [HttpPut("{key}")]
    public async Task<ActionResult<PlatformConfigurationDto>> Update(
        string key,
        [FromBody] UpdatePlatformConfigurationRequest request,
        CancellationToken ct)
        => Ok(await configuration.UpdateAsync(key, request.Value, request.Reason, ActorId(), ct));

    [HttpPut("{key}/lock")]
    public async Task<ActionResult<PlatformConfigurationDto>> SetLock(
        string key,
        [FromBody] SetPlatformConfigurationLockRequest request,
        CancellationToken ct)
        => Ok(await configuration.SetLockAsync(key, request.Locked, request.Reason, ActorId(), ct));

    private Guid ActorId()
        => currentUser.UtilisateurId
            ?? throw new UnauthorizedAccessException("Utilisateur authentifié introuvable.");
}

public sealed record UpdatePlatformConfigurationRequest(string Value, string Reason);
public sealed record SetPlatformConfigurationLockRequest(bool Locked, string Reason);
