using Einvoicing.Application.Interfaces;
using Einvoicing.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Einvoicing.Api.Services;

public sealed class RelanceBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RelanceOptions> options,
    ILogger<RelanceBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var heure = options.Value?.HeureExecutionUtc ?? 2;
            var next = ProchaineExecutionUtc(DateTime.UtcNow, heure);
            var delay = next - DateTime.UtcNow;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            logger.LogInformation("Relances auto planifiees pour {NextRunUtc}.", next);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            await ExecuterUneFoisAsync(stoppingToken);
        }
    }

    private async Task ExecuterUneFoisAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRelanceService>();
            await service.ExecuterRelancesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'execution des relances automatiques.");
        }
    }

    private static DateTime ProchaineExecutionUtc(DateTime nowUtc, int heureUtc)
    {
        var prochaine = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, heureUtc, 0, 0, DateTimeKind.Utc);
        if (prochaine <= nowUtc) prochaine = prochaine.AddDays(1);
        return prochaine;
    }
}