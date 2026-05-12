using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface IScannedDocumentRepository
{
    Task<ScannedDocument?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task AjouterAsync(ScannedDocument document, CancellationToken ct = default);
    void MettreAJour(ScannedDocument document);
    Task SauvegarderAsync(CancellationToken ct = default);
}
