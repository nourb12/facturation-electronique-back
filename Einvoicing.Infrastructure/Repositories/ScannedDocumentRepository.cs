using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Entities;
using Einvoicing.Infrastructure.Persistence;

namespace Einvoicing.Infrastructure.Repositories;

public sealed class ScannedDocumentRepository(ContextBaseDeDonnees db) : IScannedDocumentRepository
{
    public Task<ScannedDocument?> ObtenirParIdAsync(Guid id, CancellationToken ct = default)
        => db.ScannedDocuments.FindAsync([id], ct).AsTask();

    public Task AjouterAsync(ScannedDocument document, CancellationToken ct = default)
        => db.ScannedDocuments.AddAsync(document, ct).AsTask();

    public void MettreAJour(ScannedDocument document)
        => db.ScannedDocuments.Update(document);

    public Task SauvegarderAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
