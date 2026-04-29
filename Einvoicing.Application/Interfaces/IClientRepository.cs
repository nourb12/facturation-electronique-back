using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IClientRepository
{
    Task<Client?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<Client?> ObtenirParEmailAsync(Guid entrepriseId, string email, CancellationToken ct = default);
    Task<(List<Client> Items, int Total)> ListerAsync(Guid entrepriseId, int page, int parPage, bool? actifSeulement, CancellationToken ct = default);
    Task<List<Client>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default);
    Task AjouterAsync(Client client, CancellationToken ct = default);
    void MettreAJour(Client client);
    Task SauvegarderAsync(CancellationToken ct = default);
}
