using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface ISignatureService
{
    Task<SignatureDto> DemanderSignatureAsync(Guid factureId, Guid entrepriseId, Guid demandeePar, CancellationToken ct = default);
    Task<SignatureDto> ObtenirStatutAsync(Guid factureId, Guid entrepriseId, CancellationToken ct = default);
    Task<SignatureDto> RelancerAsync(Guid signatureId, Guid entrepriseId, CancellationToken ct = default);
}
