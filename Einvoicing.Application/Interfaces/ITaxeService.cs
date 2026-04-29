using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface ITaxeService
{
    Task<TaxeDto> CreerAsync(Guid entrepriseId, CreerTaxeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TaxeDto>> ListerAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<TaxeDto> MettreAJourAsync(Guid id, Guid entrepriseId, MettreAJourTaxeRequest request, CancellationToken ct = default);
    Task SupprimerAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
}