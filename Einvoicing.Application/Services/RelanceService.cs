using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Options;
using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Einvoicing.Application.Services;

public sealed class RelanceService(
    IEntrepriseRepository entrepriseRepo,
    IFactureRepository factureRepo,
    IClientRepository clientRepo,
    IEmailService emailService,
    ILatePaymentPredictionService latePaymentPredictionService,
    IOptions<RelanceOptions> options,
    ILogger<RelanceService> logger
) : IRelanceService
{
    public async Task EnvoyerRelancePredictiveAsync(
        Guid factureId,
        Guid entrepriseId,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var facture = await factureRepo.ObtenirAvecDetailsAsync(factureId, ct)
            ?? throw new ValidationMetierException("Facture introuvable.");

        if (facture.EntrepriseId != entrepriseId || facture.TypeFacture != TypeFacture.Facture)
            throw new ValidationMetierException("Cette facture ne peut pas recevoir de relance.");

        if (facture.MontantRestant <= 0)
            throw new ValidationMetierException("Cette facture est déjà réglée.");

        var prediction = await latePaymentPredictionService.PredireAsync(factureId, entrepriseId, ct);
        if (prediction.ProbabiliteRetard < 40)
            throw new ValidationMetierException("Le risque ML est faible : aucune relance n'est recommandée.");

        var client = await clientRepo.ObtenirParIdAsync(facture.ClientId, ct);
        if (client is null || string.IsNullOrWhiteSpace(client.Email))
            throw new ValidationMetierException("Le client ne possède pas d'adresse e-mail pour la relance.");

        const string action = "Relance prédictive";
        if (await factureRepo.RelanceDejaEnvoyeeAsync(facture.Id, action, ct))
            throw new ValidationMetierException("Une relance prédictive a déjà été envoyée pour cette facture.");

        var sujet = $"Rappel de règlement - Facture {facture.Numero}";
        var html = $"""
            <p>Bonjour {System.Net.WebUtility.HtmlEncode(client.Nom)},</p>
            <p>Nous vous rappelons que la facture <strong>{System.Net.WebUtility.HtmlEncode(facture.Numero)}</strong>
            présente un montant restant de <strong>{facture.MontantRestant:N3} {System.Net.WebUtility.HtmlEncode(facture.Devise)}</strong>.</p>
            <p>Merci de bien vouloir procéder au règlement ou nous contacter pour toute question.</p>
            <p>Cordialement,<br/>TuniFlow</p>
            """;

        await emailService.SendEmailAsync(client.Email, sujet, html, ct);
        await factureRepo.EnregistrerRelanceAsync(
            facture.Id,
            utilisateurId,
            action,
            $"Relance prédictive envoyée à {client.Email} (risque {prediction.ProbabiliteRetard:N0}%).",
            ct);
        await factureRepo.SauvegarderAsync(ct);
    }

    public async Task ExecuterRelancesAsync(CancellationToken ct = default)
    {
        var opt = options.Value ?? new RelanceOptions();
        if (!opt.Actif)
        {
            logger.LogInformation("Relances automatiques desactivees.");
            return;
        }

        var delais = opt.DelaisJours
            .Where(d => d > 0)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (delais.Count == 0) delais = new List<int> { 7, 14, 30 };

        var entreprises = await entrepriseRepo.ListerToutesAsync(ct);
        foreach (var ent in entreprises)
        {
            var factures = await factureRepo.ListerEnRetardAsync(ent.Id, ct);
            if (factures.Count == 0) continue;

            foreach (var f in factures)
            {
                if (f.TypeFacture != TypeFacture.Facture) continue;
                if (f.MontantRestant <= 0) continue;

                var jours = (DateTime.UtcNow.Date - f.DateEcheance.Date).Days;
                if (!delais.Contains(jours)) continue;

                var action = $"Relance J+{jours}";
                if (await factureRepo.RelanceDejaEnvoyeeAsync(f.Id, action, ct)) continue;

                var client = await clientRepo.ObtenirParIdAsync(f.ClientId, ct);
                if (client == null || string.IsNullOrWhiteSpace(client.Email))
                {
                    logger.LogWarning("Relance ignoree pour facture {Numero} (client email manquant).", f.Numero);
                    continue;
                }

                var sujet = BuildSubject(opt.SujetTemplate, jours, f.Numero, client.Nom);

                await emailService.EnvoyerNotifFactureAsync(
                    client.Email,
                    client.Nom,
                    sujet,
                    f.Numero,
                    opt.TypeNotif ?? "rappel",
                    f.MontantRestant,
                    ct);

                await factureRepo.EnregistrerRelanceAsync(
                    f.Id,
                    Guid.Empty,
                    action,
                    $"Relance automatique {action} envoyee a {client.Email}.",
                    ct);

                await factureRepo.SauvegarderAsync(ct);

                logger.LogInformation("Relance envoyee: {Action} pour facture {Numero}.", action, f.Numero);
            }
        }
    }

    private static string BuildSubject(string template, int delai, string numero, string client)
    {
        if (string.IsNullOrWhiteSpace(template))
            return $"Relance J+{delai} - Facture {numero}";

        return template
            .Replace("{delai}", delai.ToString())
            .Replace("{numero}", numero)
            .Replace("{client}", client);
    }
}
