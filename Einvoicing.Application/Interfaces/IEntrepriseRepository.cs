using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IEntrepriseRepository
{
    Task<Entreprise?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<Entreprise?> ObtenirParMatriculeAsync(string matricule, CancellationToken ct = default);
    Task<List<Entreprise>> ListerToutesAsync(CancellationToken ct = default);
    Task AjouterAsync(Entreprise entreprise, CancellationToken ct = default);
    void MettreAJour(Entreprise entreprise);
    Task SauvegarderAsync(CancellationToken ct = default);
}
