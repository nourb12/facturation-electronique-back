using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IFournisseurRepository
{
    Task<Fournisseur?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<Fournisseur?> ObtenirParMatriculeFiscalAsync(Guid entrepriseId, string matriculeFiscal, CancellationToken ct = default);
    Task<List<Fournisseur>> RechercherAsync(Guid entrepriseId, string terme, CancellationToken ct = default);
    Task AjouterAsync(Fournisseur fournisseur, CancellationToken ct = default);
    void MettreAJour(Fournisseur fournisseur);
    Task SauvegarderAsync(CancellationToken ct = default);
}
