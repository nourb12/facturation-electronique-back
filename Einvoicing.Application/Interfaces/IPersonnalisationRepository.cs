using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IPersonnalisationRepository
{
    Task<Personnalisation?> ObtenirAsync(Guid entrepriseId, CancellationToken ct = default);
    Task AjouterAsync(Personnalisation personnalisation, CancellationToken ct = default);
    void MettreAJour(Personnalisation personnalisation);
    Task SauvegarderAsync(CancellationToken ct = default);
}