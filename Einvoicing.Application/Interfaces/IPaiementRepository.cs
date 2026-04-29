using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IPaiementRepository
{
    Task<Paiement?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Paiement>> ListerParFactureAsync(Guid factureId, CancellationToken ct = default);
    Task<(List<Paiement> Items, int Total)> ListerParEntrepriseAsync(Guid entrepriseId, int page, int parPage, CancellationToken ct = default);
    Task AjouterAsync(Paiement paiement, CancellationToken ct = default);
    Task SauvegarderAsync(CancellationToken ct = default);
}
