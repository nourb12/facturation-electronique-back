using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IParametreFiscalService
{
    Task<ParametreFiscalDto> CreerAsync(Guid entrepriseId, CreerParametreFiscalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ParametreFiscalDto>> ListerAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<ParametreFiscalDto> MettreAJourAsync(Guid id, Guid entrepriseId, MettreAJourParametreFiscalRequest request, CancellationToken ct = default);
    Task SupprimerAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
}