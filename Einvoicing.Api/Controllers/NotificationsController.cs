using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Einvoicing.Application.Interfaces;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(
    IEmailService emailService,
    ISmsService smsService,
    ILogger<NotificationsController> logger) : ControllerBase
{
    [HttpPost("email/bienvenue")]
    [AllowAnonymous]
    public async Task<IActionResult> EmailBienvenue([FromBody] BienvenueRequest req)
    {
        try
        {
            await emailService.EnvoyerBienvenueEntrepriseAsync(
                req.Email,
                req.Prenom,
                req.Nom,
                req.NomEntreprise,
                req.MatriculeFiscal,
                req.Role
            );
            return Ok(new { message = "Email envoye." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur envoi email bienvenue {Email}", req.Email);
            return Ok(new { message = "Email non envoye (mode developpement)." });
        }
    }

    [HttpPost("sms/verification")]
    [AllowAnonymous]
    public async Task<IActionResult> SmsVerification([FromBody] SmsVerifRequest req)
    {
        try
        {
            await smsService.EnvoyerAsync(
                req.Telephone,
                $"[TunisFlow] Code de verification : {req.Code}. Valable 10 minutes."
            );
            return Ok(new { message = "SMS envoye." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur envoi SMS {Tel}", req.Telephone);
            return Ok(new { message = "SMS non envoye (mode developpement)." });
        }
    }

    [HttpPost("facture")]
    public async Task<IActionResult> NotifFacture([FromBody] NotifFactureRequest req)
    {
        var sujet = req.Type switch
        {
            "validee" => "Facture validee - TunisFlow",
            "rejetee" => "Facture rejetee - Action requise",
            "payee" => "Paiement recu - TunisFlow",
            "rappel" => "Rappel echeance facture",
            _ => "Notification TunisFlow"
        };

        try
        {
            await emailService.EnvoyerNotifFactureAsync(
                req.Email,
                req.NomClient,
                sujet,
                req.NumeroFacture,
                req.Type,
                req.MontantTtc
            );

            if (!string.IsNullOrWhiteSpace(req.Telephone))
            {
                var sms = req.Type switch
                {
                    "validee" => $"[TunisFlow] Facture {req.NumeroFacture} validee. Montant: {req.MontantTtc:N3} TND.",
                    "rejetee" => $"[TunisFlow] Facture {req.NumeroFacture} rejetee. Connectez-vous pour voir les details.",
                    "payee" => $"[TunisFlow] Paiement recu pour facture {req.NumeroFacture}. Merci.",
                    "rappel" => $"[TunisFlow] Rappel: Facture {req.NumeroFacture} ({req.MontantTtc:N3} TND) arrive a echeance.",
                    _ => $"[TunisFlow] Mise a jour facture {req.NumeroFacture}."
                };
                await smsService.EnvoyerAsync(req.Telephone, sms);
            }

            return Ok(new { message = "Notification envoyee." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur notification facture {NumFac}", req.NumeroFacture);
            return Ok(new { message = "Notification non envoyee (mode developpement)." });
        }
    }
}

public record BienvenueRequest(
    string Email, string Prenom, string Nom,
    string NomEntreprise, string MatriculeFiscal, string Role);

public record SmsVerifRequest(string Telephone, string Code);

public record NotifFactureRequest(
    string Email, string Telephone, string NomClient,
    string NumeroFacture, string Type, decimal MontantTtc);