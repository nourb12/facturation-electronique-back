using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface ICategorieService
{
    Task<CategorieDto> CreerAsync(Guid entrepriseId, CreerCategorieRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CategorieDto>> ListerAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<CategorieDto> MettreAJourAsync(Guid id, Guid entrepriseId, MettreAJourCategorieRequest request, CancellationToken ct = default);
    Task DesactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
}
