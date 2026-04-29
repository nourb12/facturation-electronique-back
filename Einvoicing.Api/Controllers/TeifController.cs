




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/teif")]
[Produces("application/json")]
[Authorize]
public sealed class TeifController(
    ITeifService teifService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private IActionResult? EntrepriseRequise(out Guid entrepriseId, out Guid utilisateurId)
    {
        entrepriseId = Guid.Empty;
        utilisateurId = Guid.Empty;

        if (currentUser.EntrepriseId is null)
            return Problem(
                title: "Entreprise non rattachée",
                detail: "Votre compte n'est pas encore rattaché à une entreprise. " +
                        "Veuillez compléter l'onboarding ou vous reconnecter.",
                statusCode: 403);

        if (currentUser.UtilisateurId is null)
            return Problem(title: "Utilisateur invalide", statusCode: 401);

        entrepriseId = currentUser.EntrepriseId.Value;
        utilisateurId = currentUser.UtilisateurId.Value;
        return null;
    }

    [HttpPost("{factureId:guid}/generer-xml")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(XmlTeifDto), 200)]
    public async Task<IActionResult> GenererXml(Guid factureId, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var result = await teifService.GenererXmlAsync(factureId, eId, ct);
        return Ok(result);
    }

    [HttpGet("{factureId:guid}/generer-xml/fichier")]
    [Authorize(Policy = "Tous")]
    public async Task<IActionResult> TelechargerXml(Guid factureId, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var result = await teifService.GenererXmlAsync(factureId, eId, ct);
        var bytes = System.Text.Encoding.UTF8.GetBytes(result.XmlContent);
        return File(bytes, "application/xml", $"{result.Numero}_TEIF.xml");
    }

    [HttpPost("{factureId:guid}/valider")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(ValidationTeifDto), 200)]
    public async Task<IActionResult> ValiderConformite(Guid factureId, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var result = await teifService.ValiderConformiteAsync(factureId, eId, ct);
        return Ok(result);
    }

    [HttpPost("{factureId:guid}/marquer-conforme")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FactureDto), 200)]
    public async Task<IActionResult> MarquerConforme(Guid factureId, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await teifService.MarquerConformeAsync(factureId, eId, uId, ct);
        return Ok(result);
    }
}
