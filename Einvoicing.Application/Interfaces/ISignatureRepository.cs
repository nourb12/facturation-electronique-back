using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface ISignatureRepository
{
    Task<SignatureRequest?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<SignatureRequest?> ObtenirParFactureAsync(Guid factureId, CancellationToken ct = default);
    Task AjouterAsync(SignatureRequest signature, CancellationToken ct = default);
    void MettreAJour(SignatureRequest signature);
    Task SauvegarderAsync(CancellationToken ct = default);
}
