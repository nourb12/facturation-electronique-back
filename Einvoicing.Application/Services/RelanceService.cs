using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Options;
using Einvoicing.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Einvoicing.Application.Services;

public sealed class RelanceService(
    IEntrepriseRepository entrepriseRepo,
    IFactureRepository factureRepo,
    IClientRepository clientRepo,
    IEmailService emailService,
    IOptions<RelanceOptions> options,
    ILogger<RelanceService> logger
) : IRelanceService
{
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