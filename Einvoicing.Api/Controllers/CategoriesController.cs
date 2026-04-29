




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
[Authorize]
public sealed class CategoriesController(
    ICategorieService categorieService,
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

    [HttpPost]
    [ProducesResponseType(typeof(CategorieDto), 201)]
    public async Task<IActionResult> Creer([FromBody] CreerCategorieRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await categorieService.CreerAsync(eId, req, ct);
        return CreatedAtAction(nameof(Lister), result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategorieDto>), 200)]
    public async Task<IActionResult> Lister(CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await categorieService.ListerAsync(eId, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategorieDto), 200)]
    public async Task<IActionResult> MettreAJour(
        Guid id, [FromBody] MettreAJourCategorieRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await categorieService.MettreAJourAsync(id, eId, req, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/desactiver")]
    public async Task<IActionResult> Desactiver(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        await categorieService.DesactiverAsync(id, eId, ct);
        return Ok(new { message = "Catégorie désactivée." });
    }
}
