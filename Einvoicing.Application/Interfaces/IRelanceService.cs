namespace Einvoicing.Application.Interfaces;

public interface IRelanceService
{
    Task ExecuterRelancesAsync(CancellationToken ct = default);
}