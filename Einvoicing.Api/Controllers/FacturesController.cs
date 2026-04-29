




using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/factures")]
[Produces("application/json")]
[Authorize]
public sealed class FacturesController(
    IFactureService factureService,
    ICurrentUserService currentUser
) : ControllerBase
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
                detail: "Votre compte n'est pas encore rattaché à une entreprise. " +
                        "Veuillez compléter l'onboarding ou vous reconnecter.",
                statusCode: 403);

        if (UtilisateurIdOpt is null)
            return Problem(title: "Utilisateur invalide", statusCode: 401);

        entrepriseId = EntrepriseIdOpt.Value;
        utilisateurId = UtilisateurIdOpt.Value;
        return null;
    }

    [HttpPost]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FactureDto), 201)]
    public async Task<IActionResult> Creer(
        [FromBody] CreerFactureRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await factureService.CreerAsync(eId, uId, req, ct);
        return CreatedAtAction(nameof(ObtenirParId), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListeFacturesDto), 200)]
    public async Task<IActionResult> Lister(
        [FromQuery] int page = 1,
        [FromQuery] int parPage = 20,
        [FromQuery] Guid? clientId = null,
        [FromQuery] Domain.Enums.StatutFacture? statut = null,
        [FromQuery] Domain.Enums.TypeFacture? typeFacture = null,
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        [FromQuery] decimal? montantMin = null,
        [FromQuery] decimal? montantMax = null,
        [FromQuery] string? recherche = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var filtre = new FiltreFacturesRequest(
            page, parPage, clientId, statut,
            dateDebut, dateFin, montantMin, montantMax, recherche, typeFacture);
        var result = await factureService.ListerAsync(eId, filtre, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FactureDto), 200)]
    public async Task<IActionResult> ObtenirParId(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var result = await factureService.ObtenirParIdAsync(id, eId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/historique")]
    [ProducesResponseType(typeof(IReadOnlyList<HistoriqueFactureDto>), 200)]
    public async Task<IActionResult> ObtenirHistorique(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var result = await factureService.ObtenirHistoriqueAsync(id, eId, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FactureDto), 200)]
    public async Task<IActionResult> MettreAJour(
        Guid id, [FromBody] MettreAJourFactureRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await factureService.MettreAJourAsync(id, eId, uId, req, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/valider")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FactureDto), 200)]
    public async Task<IActionResult> Valider(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await factureService.ValiderAsync(id, eId, uId, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/rejeter")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FactureDto), 200)]
    public async Task<IActionResult> Rejeter(
        Guid id, [FromBody] RejeterFactureRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await factureService.RejeterAsync(id, eId, uId, req, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/annuler")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FactureDto), 200)]
    public async Task<IActionResult> Annuler(
        Guid id, [FromBody] AnnulerFactureRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await factureService.AnnulerAsync(id, eId, uId, req, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/remettre-brouillon")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(FactureDto), 200)]
    public async Task<IActionResult> RemettreBrouillon(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await factureService.RemettreBrouillonAsync(id, eId, uId, ct);
        return Ok(result);
    }

    [HttpGet("statistiques")]
    [ProducesResponseType(typeof(StatistiquesFacturesDto), 200)]
    public async Task<IActionResult> Statistiques(CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var result = await factureService.ObtenirStatistiquesAsync(eId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/pdf")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> TelechargerPdf(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var pdfBytes = await factureService.GenererPdfAsync(id, eId, ct);
        return File(pdfBytes, "application/pdf", "facture-" + id + ".pdf");
    }
}
