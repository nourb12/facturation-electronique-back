using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using System.Globalization;

namespace Einvoicing.Application.Services;

public sealed class RapportService(
    IFactureRepository factureRepo
) : IRapportService
{
    public Task<List<TvaParTauxDto>> ObtenirTvaParTauxAsync(
        Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default)
        => factureRepo.ObtenirRepartitionTvaAsync(entrepriseId, debut, fin, ct);

    public Task<List<DelaiPaiementClientDto>> ObtenirDelaisPaiementAsync(
        Guid entrepriseId, DateTime debut, DateTime fin, int top = 5, CancellationToken ct = default)
        => factureRepo.ObtenirDelaisPaiementAsync(entrepriseId, debut, fin, top, ct);

    public async Task<List<RecapMensuelDto>> ObtenirRecapMensuelAsync(
        Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default)
    {
        var culture = CultureInfo.GetCultureInfo("fr-TN");
        var baseData = await factureRepo.ObtenirRecapMensuelAsync(entrepriseId, debut, fin, ct);
        var map = baseData.ToDictionary(r => r.Mois, r => r);

        var debutMois = new DateTime(debut.Year, debut.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var finMois = new DateTime(fin.Year, fin.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (fin > finMois) finMois = finMois.AddMonths(1);

        var result = new List<RecapMensuelDto>();
        for (var m = debutMois; m < finMois; m = m.AddMonths(1))
        {
            var label = m.ToString("MMM yyyy", culture);
            if (map.TryGetValue(label, out var dto))
            {
                result.Add(dto);
                continue;
            }

            result.Add(new RecapMensuelDto(label, 0, 0, 0, 0, 0, 0, 0));
        }

        return result;
    }
}