using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IEchangeRepository
{
    Task<ExternalExchange?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<ExternalExchange?> ObtenirDernierParFactureAsync(Guid factureId, CancellationToken ct = default);
    Task<List<ExternalExchange>> ListerParFactureAsync(Guid factureId, CancellationToken ct = default);
    Task AjouterAsync(ExternalExchange echange, CancellationToken ct = default);
    void MettreAJour(ExternalExchange echange);
    Task SauvegarderAsync(CancellationToken ct = default);
}
