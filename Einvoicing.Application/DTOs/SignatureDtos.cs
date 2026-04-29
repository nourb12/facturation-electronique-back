namespace Einvoicing.Application.DTOs;

public record DemanderSignatureRequest(Guid FactureId);

public record SignatureDto(
    Guid Id,
    Guid FactureId,
    string Statut,
    string? SignatureValue,
    string? CertificatId,
    string? MessageErreur,
    int NbTentatives,
    DateTime CreeLe,
    DateTime? SigneeA
);
