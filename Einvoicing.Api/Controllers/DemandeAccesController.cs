using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

/// <summary>
/// Gestion des demandes d'accès entreprise avec validation administrative.
/// Écran 1 (soumission) → Écran 2 (confirmation) → Écran 3 (attente) → Écran 4 (accès).
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class DemandeAccesController(
    IDemandeAccesService demandeService,
    ICurrentUserService currentUser
) : ControllerBase
{
    // —— Écran 1 — Soumission de la demande d'accès —————————————
    [HttpPost("demande-acces")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DemandeAccesConfirmationDto), 200)]
    public async Task<IActionResult> SoumettreDemandeAcces(
        [FromForm] SoumettreDemandeAccesRequest req,
        CancellationToken ct)
    {
        var result = await demandeService.SoumettreDemandeAsync(req, ct);
        return Ok(result);
    }

    // —— Écran 3 — Vérifier le statut du compte (appelé par le bouton) ————
    [HttpGet("statut-compte")]
    [Authorize]
    [ProducesResponseType(typeof(StatutCompteDto), 200)]
    public async Task<IActionResult> VerifierStatutCompte(CancellationToken ct)
    {
        var userId = currentUser.UtilisateurId
            ?? throw new AccesRefuseException("Utilisateur non authentifié.");

        var statut = await demandeService.VerifierStatutAsync(userId, ct);
        return Ok(statut);
    }

    // —— Admin — Lister les demandes en attente ———————————————————————
    [HttpGet("demandes-acces")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<DemandeAccesDto>), 200)]
    public async Task<IActionResult> ListerDemandesEnAttente(
        [FromQuery] string? statut = "EnAttente",
        CancellationToken ct = default)
    {
        var result = await demandeService.ListerDemandesAsync(statut, ct);
        return Ok(result);
    }

    // —— Admin — Valider une demande et envoyer les identifiants ————————
    [HttpPost("demandes-acces/{id:guid}/valider")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ValiderDemande(
        Guid id, CancellationToken ct)
    {
        await demandeService.ValiderDemandeAsync(id, ct);
        return Ok(new { message = "Demande validée. Email d'accès envoyé à l'entreprise." });
    }

    // —— Admin — Rejeter une demande ————————————————————————————————
    [HttpPost("demandes-acces/{id:guid}/rejeter")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> RejeterDemande(
        Guid id, [FromBody] RejeterDemandeRequest req,
        CancellationToken ct)
    {
        await demandeService.RejeterDemandeAsync(id, req.Motif, ct);
        return Ok(new { message = "Demande rejetée. Email de notification envoyé." });
    }
}

/// <summary>Request pour le rejet d'une demande.</summary>
public record RejeterDemandeRequest(string Motif);