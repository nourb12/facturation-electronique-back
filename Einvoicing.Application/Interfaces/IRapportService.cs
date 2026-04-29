using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IRapportService
{
    Task<List<TvaParTauxDto>> ObtenirTvaParTauxAsync(Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default);
    Task<List<DelaiPaiementClientDto>> ObtenirDelaisPaiementAsync(Guid entrepriseId, DateTime debut, DateTime fin, int top = 5, CancellationToken ct = default);
    Task<List<RecapMensuelDto>> ObtenirRecapMensuelAsync(Guid entrepriseId, DateTime debut, DateTime fin, CancellationToken ct = default);
}
