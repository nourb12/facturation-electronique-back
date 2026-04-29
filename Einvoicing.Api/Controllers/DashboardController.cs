using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
[Authorize]
public sealed class DashboardController(
    IDashboardService dashboardService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private Guid EntrepriseId => currentUser.EntrepriseId!.Value;

    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), 200)]
    public async Task<IActionResult> ObtenirDashboard(CancellationToken ct)
    {
        var result = await dashboardService.ObtenirDashboardEntrepriseAsync(EntrepriseId, ct);
        return Ok(result);
    }

    [HttpGet("admin")]
    [Authorize(Policy = "SuperAdmin")]
    [ProducesResponseType(typeof(DashboardAdminDto), 200)]
    public async Task<IActionResult> ObtenirDashboardAdmin(CancellationToken ct)
    {
        var result = await dashboardService.ObtenirDashboardAdminAsync(ct);
        return Ok(result);
    }
}
