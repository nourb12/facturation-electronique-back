using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IEntrepriseService
{
    Task<EntrepriseDto> CreerAsync(CreerEntrepriseRequest request, Guid adminId, CancellationToken ct = default);
    Task<EntrepriseDto> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<EntrepriseDto> MettreAJourAsync(Guid id, MettreAJourEntrepriseRequest request, CancellationToken ct = default);
    Task ConfigurerTeifAsync(Guid id, ConfigurerTeifRequest request, CancellationToken ct = default);
    Task DesactiverAsync(Guid id, CancellationToken ct = default);
    Task ReactiverAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EntrepriseDto>> ListerToutesAsync(CancellationToken ct = default);
}
