using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IClientService
{
    Task<ClientDto> CreerAsync(Guid entrepriseId, CreerClientRequest request, CancellationToken ct = default);
    Task<ClientDto> ObtenirParIdAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task<ListeClientsDto> ListerAsync(Guid entrepriseId, int page, int parPage, bool? actifSeulement, CancellationToken ct = default);
    Task<ClientDto> MettreAJourAsync(Guid id, Guid entrepriseId, MettreAJourClientRequest request, CancellationToken ct = default);
    Task DesactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task ReactiverAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task<IReadOnlyList<ClientDto>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default);
}
