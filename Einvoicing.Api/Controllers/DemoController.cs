using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Domain.Enums;
using Einvoicing.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;

namespace Einvoicing.Api.Controllers;

/// <summary>
/// Contrôleur pour la gestion des réservations de démonstration
/// </summary>
[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IDemoRequestRepository _demoRepository;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        IEmailService emailService,
        IDemoRequestRepository demoRepository,
        ILogger<DemoController> logger)
    {
        _emailService = emailService;
        _demoRepository = demoRepository;
        _logger = logger;
    }

    /// <summary>
    /// Obtenir toutes les demandes de démo
    /// </summary>
    [Authorize(Policy = "SuperOuAdmin")]
    [HttpGet("requests")]
    public async Task<IActionResult> GetAllRequests()
    {
        try
        {
            var requests = await _demoRepository.GetAllAsync();
            return Ok(requests.Select(r => new
            {
                id = r.Id,
                firstName = r.FirstName,
                lastName = r.LastName,
                email = r.Email,
                company = r.Company,
                phone = r.Phone,
                message = r.Message,
                preferredDate = r.PreferredDate.ToString("yyyy-MM-dd"),
                preferredTime = r.PreferredTime,
                status = r.Status.ToString().ToLowerInvariant(),
                createdAt = r.CreatedAt.ToString("o")
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des demandes de démo");
            return StatusCode(500, new { success = false, message = "Une erreur est survenue" });
        }
    }

    /// <summary>
    /// Obtenir une demande de démo par ID
    /// </summary>
    [Authorize(Policy = "SuperOuAdmin")]
    [HttpGet("requests/{id}")]
    public async Task<IActionResult> GetRequestById(Guid id)
    {
        try
        {
            var request = await _demoRepository.GetByIdAsync(id);
            if (request == null)
                return NotFound(new { success = false, message = "Demande non trouvée" });

            return Ok(new
            {
                id = request.Id,
                firstName = request.FirstName,
                lastName = request.LastName,
                email = request.Email,
                company = request.Company,
                phone = request.Phone,
                message = request.Message,
                preferredDate = request.PreferredDate.ToString("yyyy-MM-dd"),
                preferredTime = request.PreferredTime,
                status = request.Status.ToString().ToLowerInvariant(),
                createdAt = request.CreatedAt.ToString("o")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de la demande {Id}", id);
            return StatusCode(500, new { success = false, message = "Une erreur est survenue" });
        }
    }

    /// <summary>
    /// Mettre à jour le statut d'une demande
    /// </summary>
    [Authorize(Policy = "SuperOuAdmin")]
    [HttpPatch("requests/{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        try
        {
            var request = await _demoRepository.GetByIdAsync(id);
            if (request == null)
                return NotFound(new { success = false, message = "Demande non trouvée" });

            if (!Enum.TryParse<DemoRequestStatus>(dto.Status, true, out var newStatus))
                return BadRequest(new { success = false, message = "Statut invalide" });

            request.UpdateStatus(newStatus);
            _demoRepository.Update(request);
            await _demoRepository.SaveChangesAsync();

            _logger.LogInformation(
                "Statut de la demande {Id} mis à jour : {OldStatus} -> {NewStatus}",
                id, request.Status, newStatus
            );

            return Ok(new { success = true, message = "Statut mis à jour" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour du statut de la demande {Id}", id);
            return StatusCode(500, new { success = false, message = "Une erreur est survenue" });
        }
    }

    /// <summary>
    /// Réserver une session de démonstration
    /// </summary>
    /// <param name="dto">Informations de réservation</param>
    /// <returns>Confirmation de réservation</returns>
    [AllowAnonymous]
    [HttpPost("book")]
    public async Task<IActionResult> BookDemo([FromBody] DemoBookingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Créer l'entité DemoRequest
            var demoRequest = DemoRequest.Create(
                dto.Prenom,
                dto.Nom,
                dto.Email,
                dto.Entreprise,
                dto.Date,
                dto.Heure,
                dto.Telephone,
                dto.Message
            );

            await _demoRepository.AddAsync(demoRequest);
            await _demoRepository.SaveChangesAsync();

            var dateStr = dto.Date.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR"));

            // Email au demandeur
            await _emailService.SendEmailAsync(
                dto.Email,
                "Confirmation de votre démo EY-Factify",
                BuildConfirmationEmail(dto, dateStr)
            );

            // Email interne à l'équipe
            await _emailService.SendEmailAsync(
                "noreply.einvoicingportal@gmail.com", // Remplacer par l'email de l'équipe EY
                $"[Démo] {dto.Prenom} {dto.Nom} — {dto.Entreprise} — {dateStr} {dto.Heure}",
                BuildInternalEmail(dto, dateStr)
            );

            _logger.LogInformation(
                "Réservation de démo confirmée : {Email} - {Entreprise} - {Date} {Heure}",
                dto.Email, dto.Entreprise, dateStr, dto.Heure
            );

            return Ok(new { success = true, message = "Réservation confirmée", id = demoRequest.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la réservation de démo pour {Email}", dto.Email);
            return StatusCode(500, new { success = false, message = "Une erreur est survenue" });
        }
    }

    private static string BuildConfirmationEmail(DemoBookingDto dto, string dateStr) => $"""
        <!DOCTYPE html>
        <html lang="fr">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Confirmation démo EY-Factify</title>
        </head>
        <body style="font-family: 'DM Sans', Arial, sans-serif; background:#f9f9f9; margin:0; padding:32px;">
            <div style="max-width:560px; margin:0 auto; background:#fff; border-radius:16px; overflow:hidden; border:1px solid #eee;">
                <!-- Header -->
                <div style="background:#FFE600; padding:20px 32px;">
                    <span style="font-size:16px; font-weight:800; color:#000;">EY-Factify</span>
                </div>
                
                <!-- Body -->
                <div style="padding:32px;">
                    <h2 style="font-size:20px; color:#0A0A0A; margin-bottom:8px;">Votre démo est confirmée !</h2>
                    <p style="color:#555; line-height:1.8;">Bonjour {dto.Prenom},</p>
                    <p style="color:#555; line-height:1.8;">
                        Votre session de démonstration EY-Factify a bien été enregistrée.
                    </p>
                    
                    <!-- Details box -->
                    <div style="background:#f5f5f5; border-radius:12px; padding:18px 20px; margin:20px 0;">
                        <p style="margin:0 0 8px; font-size:13px; color:#888;">DÉTAILS DE LA SESSION</p>
                        <p style="margin:4px 0; font-size:14px; color:#0A0A0A;"><strong>Date :</strong> {dateStr}</p>
                        <p style="margin:4px 0; font-size:14px; color:#0A0A0A;"><strong>Heure :</strong> {dto.Heure}</p>
                        <p style="margin:4px 0; font-size:14px; color:#0A0A0A;"><strong>Durée :</strong> 30 minutes</p>
                        <p style="margin:4px 0; font-size:14px; color:#0A0A0A;"><strong>Support :</strong> Microsoft Teams</p>
                        <p style="margin:4px 0; font-size:14px; color:#0A0A0A;"><strong>Entreprise :</strong> {dto.Entreprise}</p>
                    </div>
                    
                    <p style="color:#555; line-height:1.8; font-size:13px;">
                        Un lien Microsoft Teams vous sera envoyé 24h avant la session.<br>
                        Si vous devez annuler ou reporter, contactez-nous à 
                        <a href="mailto:demo@eyinvoice.tn" style="color:#FFE600; text-decoration:none;">demo@eyinvoice.tn</a>.
                    </p>
                </div>
                
                <!-- Footer -->
                <div style="padding:16px 32px; border-top:1px solid #eee; font-size:11px; color:#aaa; text-align:center;">
                    © 2026 EY-Factify · Ernst & Young Tunisia · Plateforme TEIF 2026
                </div>
            </div>
        </body>
        </html>
        """;

    private static string BuildInternalEmail(DemoBookingDto dto, string dateStr) => $"""
        <h3>Nouvelle réservation de démo</h3>
        <ul>
            <li><strong>Nom :</strong> {dto.Prenom} {dto.Nom}</li>
            <li><strong>Email :</strong> {dto.Email}</li>
            <li><strong>Entreprise :</strong> {dto.Entreprise}</li>
            <li><strong>Téléphone :</strong> {dto.Telephone ?? "—"}</li>
            <li><strong>Date :</strong> {dateStr} à {dto.Heure}</li>
            <li><strong>Message :</strong> {dto.Message ?? "—"}</li>
        </ul>
        """;
}
