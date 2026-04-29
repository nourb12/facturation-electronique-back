using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface ITaxeRepository
{
    Task<Taxe?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Taxe>> ListerAsync(Guid entrepriseId, CancellationToken ct = default);
    Task AjouterAsync(Taxe taxe, CancellationToken ct = default);
    void MettreAJour(Taxe taxe);
    void Supprimer(Taxe taxe);
    Task SauvegarderAsync(CancellationToken ct = default);
}