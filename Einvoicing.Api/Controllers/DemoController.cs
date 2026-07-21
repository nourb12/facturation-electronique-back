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

    /// <summary>Obtenir toutes les demandes de démo</summary>
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

    /// <summary>Obtenir une demande de démo par ID</summary>
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

    /// <summary>Mettre à jour le statut d'une demande</summary>
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
                id, request.Status, newStatus);

            return Ok(new { success = true, message = "Statut mis à jour" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour du statut de la demande {Id}", id);
            return StatusCode(500, new { success = false, message = "Une erreur est survenue" });
        }
    }

    /// <summary>Réserver une session de démonstration</summary>
    [AllowAnonymous]
    [HttpPost("book")]
    public async Task<IActionResult> BookDemo([FromBody] DemoBookingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var demoRequest = DemoRequest.Create(
                dto.Prenom, dto.Nom, dto.Email, dto.Entreprise,
                dto.Date, dto.Heure, dto.Telephone, dto.Message);

            await _demoRepository.AddAsync(demoRequest);
            await _demoRepository.SaveChangesAsync();

            var dateStr = dto.Date.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR"));

            await _emailService.SendEmailAsync(
                dto.Email,
                "Votre session de démo est confirmée — TuniFlow",
                BuildConfirmationEmail(dto, dateStr));

            await _emailService.SendEmailAsync(
                "noreply.einvoicingportal@gmail.com",
                $"[Démo] {dto.Prenom} {dto.Nom} — {dto.Entreprise} — {dateStr} {dto.Heure}",
                BuildInternalEmail(dto, dateStr));

            _logger.LogInformation(
                "Réservation de démo confirmée : {Email} - {Entreprise} - {Date} {Heure}",
                dto.Email, dto.Entreprise, dateStr, dto.Heure);

            return Ok(new { success = true, message = "Réservation confirmée", id = demoRequest.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la réservation de démo pour {Email}", dto.Email);
            return StatusCode(500, new { success = false, message = "Une erreur est survenue" });
        }
    }

    // ─── Email templates ──────────────────────────────────────────────────────

    private static string BuildConfirmationEmail(DemoBookingDto dto, string dateStr) => $"""
        <!DOCTYPE html>
        <html lang="fr">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width,initial-scale=1.0">
          <title>Confirmation démo TuniFlow</title>
        </head>
        <body style="margin:0;padding:0;background-color:#F4F4F5;font-family:Arial,Helvetica,sans-serif">
          <!-- Preview text -->
          <div style="display:none;max-height:0;overflow:hidden">Votre session du {dateStr} à {dto.Heure} est confirmée.&nbsp;&zwnj;&hairsp;&hairsp;&hairsp;&hairsp;</div>

          <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#F4F4F5;padding:32px 0">
            <tr><td align="center">
              <table width="580" cellpadding="0" cellspacing="0" border="0" style="max-width:580px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #E4E4E7">

                <!-- Yellow accent -->
                <tr><td height="4" style="background:#FFE600;font-size:0;line-height:0">&nbsp;</td></tr>

                <!-- Header -->
                <tr>
                  <td style="padding:24px 32px 20px;border-bottom:1px solid #F0F0F0">
                    <table width="100%" cellpadding="0" cellspacing="0" border="0">
                      <tr>
                        <td>
                          <table cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td style="background:#FFE600;border-radius:6px;padding:5px 11px">
                                <span style="font-size:14px;font-weight:900;color:#0A0A0A;font-family:Arial,sans-serif">TF</span>
                              </td>
                              <td style="padding-left:10px;vertical-align:middle">
                                <span style="font-size:15px;font-weight:700;color:#0A0A0A;font-family:Arial,sans-serif">TuniFlow</span>
                              </td>
                            </tr>
                          </table>
                        </td>
                        <td align="right" style="vertical-align:middle">
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>

                <!-- Body -->
                <tr>
                  <td style="padding:32px 32px 24px">

                    <!-- Success badge -->
                    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px">
                      <tr>
                        <td style="background:#F0FDF4;border-left:4px solid #16A34A;border-radius:0 8px 8px 0;padding:12px 16px">
                          <span style="font-size:13px;font-weight:700;color:#14532D;font-family:Arial,sans-serif">✓ &nbsp;Votre session de démo est confirmée !</span>
                        </td>
                      </tr>
                    </table>

                    <h1 style="font-size:22px;font-weight:700;color:#0A0A0A;margin:0 0 8px;font-family:Arial,sans-serif">Bonjour {dto.Prenom},</h1>
                    <p style="font-size:14px;color:#52525B;line-height:1.7;margin:0 0 24px;font-family:Arial,sans-serif">Votre session de démonstration TuniFlow a bien été enregistrée. Retrouvez ci-dessous les détails de votre rendez-vous.</p>

                    <!-- Session details -->
                    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#FAFAFA;border:1px solid #E4E4E7;border-radius:10px;margin:0 0 24px">
                      <tr><td style="padding:16px 20px">
                        <p style="font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:#A1A1AA;margin:0 0 12px;font-family:Arial,sans-serif">Détails de la session</p>
                        <table width="100%" cellpadding="0" cellspacing="0" border="0">
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;width:140px;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5;vertical-align:top">Date</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5;font-weight:600">{dateStr}</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5;vertical-align:top">Heure</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5;font-weight:600">{dto.Heure}</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5;vertical-align:top">Durée</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5">30 minutes</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5;vertical-align:top">Support</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F4F4F5">Microsoft Teams</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;vertical-align:top">Entreprise</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif">{dto.Entreprise}</td>
                          </tr>
                        </table>
                      </td></tr>
                    </table>

                    <!-- Teams link notice -->
                    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px">
                      <tr>
                        <td style="background:#FFFBEB;border-left:4px solid #FFE600;border-radius:0 8px 8px 0;padding:12px 16px">
                          <span style="font-size:13px;color:#78350F;font-family:Arial,sans-serif">Le lien Microsoft Teams vous sera envoyé <strong>24h avant</strong> la session.</span>
                        </td>
                      </tr>
                    </table>

                    <p style="font-size:13px;color:#71717A;margin:0;line-height:1.7;font-family:Arial,sans-serif">
                      Pour annuler ou reporter, contactez-nous à
                      <a href="mailto:demo@tuniflow.tn" style="color:#0A0A0A;font-weight:700;text-decoration:none">demo@tuniflow.tn</a>.
                    </p>

                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#FAFAFA;border-top:1px solid #F0F0F0;padding:16px 32px;text-align:center">
                    <p style="font-size:11px;color:#A1A1AA;margin:0;line-height:1.7;font-family:Arial,sans-serif">
                      © 2026 TuniFlow · Ernst &amp; Young Tunisia<br>
                      <a href="mailto:demo@tuniflow.tn" style="color:#71717A;text-decoration:none">demo@tuniflow.tn</a>
                      &nbsp;·&nbsp;
                      <a href="mailto:support@tuniflow.tn" style="color:#71717A;text-decoration:none">support@tuniflow.tn</a>
                    </p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string BuildInternalEmail(DemoBookingDto dto, string dateStr) => $"""
        <!DOCTYPE html>
        <html lang="fr">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width,initial-scale=1.0">
          <title>Nouvelle réservation démo</title>
        </head>
        <body style="margin:0;padding:0;background-color:#F4F4F5;font-family:Arial,Helvetica,sans-serif">
          <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#F4F4F5;padding:32px 0">
            <tr><td align="center">
              <table width="560" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #E4E4E7">

                <tr><td height="4" style="background:#FFE600;font-size:0;line-height:0">&nbsp;</td></tr>

                <!-- Header -->
                <tr>
                  <td style="padding:20px 28px;border-bottom:1px solid #F0F0F0">
                    <table cellpadding="0" cellspacing="0" border="0">
                      <tr>
                        <td style="background:#FFE600;border-radius:6px;padding:5px 11px">
                          <span style="font-size:13px;font-weight:900;color:#0A0A0A;font-family:Arial,sans-serif">TF</span>
                        </td>
                        <td style="padding-left:10px;vertical-align:middle">
                          <span style="font-size:14px;font-weight:700;color:#0A0A0A;font-family:Arial,sans-serif">TuniFlow</span>
                          <span style="font-size:11px;color:#A1A1AA;margin-left:6px;font-family:Arial,sans-serif">Notification interne</span>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>

                <!-- Body -->
                <tr>
                  <td style="padding:28px 28px 20px">

                    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px">
                      <tr>
                        <td style="background:#FFFBEB;border-left:4px solid #FFE600;border-radius:0 8px 8px 0;padding:11px 16px">
                          <span style="font-size:13px;font-weight:700;color:#78350F;font-family:Arial,sans-serif">🗓 &nbsp;Nouvelle réservation de démo reçue</span>
                        </td>
                      </tr>
                    </table>

                    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#FAFAFA;border:1px solid #E4E4E7;border-radius:10px">
                      <tr><td style="padding:16px 20px">
                        <table width="100%" cellpadding="0" cellspacing="0" border="0">
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;width:130px;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;vertical-align:top">Prénom</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0">{dto.Prenom}</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;vertical-align:top">Nom</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0">{dto.Nom}</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;vertical-align:top">Email</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0">
                              <a href="mailto:{dto.Email}" style="color:#0A0A0A;text-decoration:none;font-weight:600">{dto.Email}</a>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;vertical-align:top">Entreprise</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;font-weight:600">{dto.Entreprise}</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;vertical-align:top">Téléphone</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0">{dto.Telephone ?? "—"}</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;vertical-align:top">Date &amp; heure</td>
                            <td style="padding:7px 0;font-size:14px;color:#0A0A0A;font-family:Arial,sans-serif;border-bottom:1px solid #F0F0F0;font-weight:600">{dateStr} à {dto.Heure}</td>
                          </tr>
                          <tr>
                            <td style="padding:7px 0;font-size:12px;font-weight:700;color:#A1A1AA;text-transform:uppercase;letter-spacing:.04em;font-family:Arial,sans-serif;vertical-align:top">Message</td>
                            <td style="padding:7px 0;font-size:14px;color:#52525B;font-family:Arial,sans-serif;line-height:1.6">{dto.Message ?? "—"}</td>
                          </tr>
                        </table>
                      </td></tr>
                    </table>

                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#FAFAFA;border-top:1px solid #F0F0F0;padding:14px 28px;text-align:center">
                    <p style="font-size:11px;color:#A1A1AA;margin:0;font-family:Arial,sans-serif">TuniFlow · Notification interne · Ne pas répondre à cet e-mail</p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
