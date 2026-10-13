using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Services;
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

    [HttpPost("admin/preview/companies")]
    [Authorize(Policy = "SuperAdmin")]
    public IActionResult PreviewCompany([FromServices] IWebHostEnvironment host, [FromBody] PreviewCompanyRequest request)
    {
        if (!host.IsDevelopment()) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
            return BadRequest(new { message = "Le nom de l'entreprise est obligatoire." });
        var examples = AdminDashboardPreview.Create("30d", DateTimeOffset.UtcNow);
        if (examples.Companies.Any(c => c.TaxId == request.TaxId))
            return Conflict(new { message = "Ce matricule existe déjà dans la démonstration." });
        // No persistence or external side effect: the client adds this row to its preview.
        return Ok(new PreviewCompanyDto("demo-" + Guid.NewGuid().ToString("N"), request.Name.Trim(), request.TaxId, 0, null, "Starter"));
    }

    [HttpGet("admin/preview")]
    [Authorize(Policy = "SuperAdmin")]
    [ProducesResponseType(typeof(AdminDashboardPreviewDto), 200)]
    public IActionResult Preview([FromServices] IWebHostEnvironment host, [FromQuery] string period = "30d")
    {
        if (!host.IsDevelopment()) return NotFound();
        if (period is not ("7d" or "30d" or "12m"))
            return BadRequest(new { message = "Période autorisée : 7d, 30d ou 12m." });
        Response.Headers.CacheControl = "no-store";
        return Ok(AdminDashboardPreview.Create(period, DateTimeOffset.UtcNow));
    }

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
