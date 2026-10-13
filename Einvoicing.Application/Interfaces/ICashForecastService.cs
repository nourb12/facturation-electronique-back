using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface ICashForecastService
{
    Task<CashForecastDto> ForecastAsync(
        Guid entrepriseId,
        CashForecastRequestDto request,
        CancellationToken ct = default);
}
