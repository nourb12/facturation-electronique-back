using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/scan")]
[Produces("application/json")]
[Authorize]
public sealed class ScanController(
    IScanService scanService,
    ICurrentUserService currentUser) : ControllerBase
{
    private Guid? EntrepriseIdOpt => currentUser.EntrepriseId;
    private Guid? UtilisateurIdOpt => currentUser.UtilisateurId;

    private IActionResult? EntrepriseRequise(out Guid entrepriseId, out Guid utilisateurId)
    {
        entrepriseId = Guid.Empty;
        utilisateurId = Guid.Empty;

        if (EntrepriseIdOpt is null)
            return Problem(
                title: "Entreprise non rattachée",
                detail: "Votre compte n'est pas encore rattaché à une entreprise.",
                statusCode: 403);

        if (UtilisateurIdOpt is null)
            return Problem(title: "Utilisateur invalide", statusCode: 401);

        entrepriseId = EntrepriseIdOpt.Value;
        utilisateurId = UtilisateurIdOpt.Value;
        return null;
    }

    public sealed record UploadScanRequest(IFormFile File, string DocumentType);

    [HttpPost("upload")]
    [Authorize(Policy = "Tous")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ScanUploadResponseDto), 200)]
    public async Task<IActionResult> Upload([FromForm] UploadScanRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var result = await scanService.UploadAsync(
            entrepriseId,
            utilisateurId,
            request.File,
            request.DocumentType,
            ct);

        return Ok(result);
    }

    [HttpPost("validate")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(ValidateScanResponseDto), 200)]
    public async Task<IActionResult> Validate([FromBody] ValidateScanRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var result = await scanService.ValidateAsync(entrepriseId, utilisateurId, request, ct);
        return Ok(result);
    }
}
