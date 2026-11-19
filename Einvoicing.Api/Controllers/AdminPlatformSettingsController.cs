using System.Text.Json;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/admin/platform-settings")]
[Produces("application/json")]
[Authorize(Policy = "SuperOuAdmin")]
public sealed class AdminPlatformSettingsController(ContextBaseDeDonnees db) : ControllerBase
{
    private const string SettingsKey = "admin-platform-settings";

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var item = await db.ParametresPlateforme.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Cle == SettingsKey, ct);

        if (item is null) return Ok(new { settings = new { }, taxRates = Array.Empty<object>() });

        using var document = JsonDocument.Parse(item.ValeurJson);
        return Ok(document.RootElement.Clone());
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] JsonElement payload, CancellationToken ct)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "Le format des paramètres est invalide." });

        if (!payload.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "La section settings est obligatoire." });

        if (settings.TryGetProperty("sessionDuration", out var duration)
            && (!duration.TryGetInt32(out var minutes) || minutes is < 5 or > 240))
            return BadRequest(new { message = "La durée de session doit être comprise entre 5 et 240 minutes." });

        if (!WebhookIsValid(settings))
            return BadRequest(new { message = "L’URL du webhook doit commencer par https://." });

        var json = payload.GetRawText();
        if (json.Length > 65_000) return BadRequest(new { message = "Les paramètres sont trop volumineux." });

        var item = await db.ParametresPlateforme.SingleOrDefaultAsync(x => x.Cle == SettingsKey, ct);
        if (item is null)
            db.ParametresPlateforme.Add(ParametrePlateforme.Creer(SettingsKey, json));
        else
            item.MettreAJour(json);

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Paramètres enregistrés." });
    }

    private static bool WebhookIsValid(JsonElement settings)
    {
        if (!settings.TryGetProperty("webhookUrl", out var webhook)
            || webhook.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(webhook.GetString())) return true;

        return Uri.TryCreate(webhook.GetString(), UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}
