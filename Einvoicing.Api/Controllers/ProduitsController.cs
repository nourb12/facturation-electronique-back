




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/produits")]
[Produces("application/json")]
[Authorize]
public sealed class ProduitsController(
    IProduitService produitService,
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
    [ProducesResponseType(typeof(ProduitDto), 201)]
    public async Task<IActionResult> Creer([FromBody] CreerProduitRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await produitService.CreerAsync(eId, req, ct);
        return CreatedAtAction(nameof(ObtenirParId), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListeProduitsDto), 200)]
    public async Task<IActionResult> Lister(
        [FromQuery] int page = 1,
        [FromQuery] int parPage = 20,
        [FromQuery] Guid? categorieId = null,
        [FromQuery] bool? actifSeulement = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await produitService.ListerAsync(eId, page, parPage, categorieId, actifSeulement, ct);
        return Ok(result);
    }

    [HttpGet("rechercher")]
    [ProducesResponseType(typeof(IReadOnlyList<ProduitDto>), 200)]
    public async Task<IActionResult> Rechercher([FromQuery] string terme, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await produitService.RechercherAsync(eId, terme, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProduitDto), 200)]
    public async Task<IActionResult> ObtenirParId(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await produitService.ObtenirParIdAsync(id, eId, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProduitDto), 200)]
    public async Task<IActionResult> MettreAJour(
        Guid id, [FromBody] MettreAJourProduitRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        var result = await produitService.MettreAJourAsync(id, eId, req, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/desactiver")]
    public async Task<IActionResult> Desactiver(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        await produitService.DesactiverAsync(id, eId, ct);
        return Ok(new { message = "Produit désactivé." });
    }

    [HttpPost("{id:guid}/reactiver")]
    public async Task<IActionResult> Reactiver(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId);
        if (guard is not null) return guard;
        await produitService.ReactiverAsync(id, eId, ct);
        return Ok(new { message = "Produit réactivé." });
    }
}
