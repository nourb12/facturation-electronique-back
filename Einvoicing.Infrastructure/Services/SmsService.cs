






using Einvoicing.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Einvoicing.Infrastructure.Services;

public sealed class SmsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<SmsService> logger) : ISmsService
{
    public async Task EnvoyerAsync(string telephone, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(telephone) || string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Le telephone et le message SMS sont obligatoires.");

        var provider = configuration["Sms:Provider"];
        // AccountSid/AuthToken are the Twilio terms. ApiKey/ApiSecret remain as
        // a fallback for existing local configurations.
        var accountSid = configuration["Sms:AccountSid"] ?? configuration["Sms:ApiKey"];
        var authToken = configuration["Sms:AuthToken"] ?? configuration["Sms:ApiSecret"];
        var from = configuration["Sms:From"];
        if (!string.Equals(provider, "Twilio", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Le fournisseur SMS n'est pas configure. Renseignez Sms:Provider, Sms:ApiKey, Sms:ApiSecret et Sms:From.");

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(accountSid)}/Messages.json");
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = NormaliserNumeroTunisien(telephone), ["From"] = from.Trim(), ["Body"] = message.Trim()
        });

        var response = await httpClientFactory.CreateClient("Sms").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Echec envoi SMS Twilio vers {Telephone}: {StatusCode} {Error}", telephone, response.StatusCode, error);
            throw new InvalidOperationException("Le fournisseur SMS a refuse l'envoi.");
        }

        logger.LogInformation("SMS envoye par Twilio vers {Telephone}", telephone);
    }

    private static string NormaliserNumeroTunisien(string telephone)
    {
        var compact = Regex.Replace(telephone, @"[^0-9+]", string.Empty);
        if (compact.StartsWith("+")) return compact;

        var digits = Regex.Replace(compact, @"\D", string.Empty);
        if (digits.StartsWith("216")) return $"+{digits}";
        if (digits.Length == 8) return $"+216{digits}";

        throw new ArgumentException("Le numero de telephone tunisien doit contenir 8 chiffres.");
    }
}
