using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IParametreFiscalRepository
{
    Task<ParametreFiscal?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ParametreFiscal>> ListerAsync(Guid entrepriseId, CancellationToken ct = default);
    Task AjouterAsync(ParametreFiscal parametre, CancellationToken ct = default);
    void MettreAJour(ParametreFiscal parametre);
    void Supprimer(ParametreFiscal parametre);
    Task SauvegarderAsync(CancellationToken ct = default);
}