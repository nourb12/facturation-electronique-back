namespace Einvoicing.Application.DTOs;

public record FournisseurDto(
    Guid Id,
    Guid EntrepriseId,
    string Nom,
    string? MatriculeFiscal,
    string? Adresse,
    string? Iban,
    string? Email,
    string? Telephone,
    bool EstActif,
    DateTime CreeLe,
    DateTime ModifieLe
);

public record FournisseurMatchCandidateDto(
    Guid Id,
    string Nom,
    string? MatriculeFiscal,
    string? Adresse,
    string? Iban,
    int Score,
    bool Exact
);

public record MatchOrCreateFournisseurRequest(
    string? Nom,
    string? MatriculeFiscal,
    string? Adresse,
    string? Iban,
    bool CreateIfMissing = false,
    Guid? SelectedSupplierId = null
);

public record FournisseurMatchResultDto(
    bool Matched,
    bool AutoSelected,
    bool Created,
    string MatchType,
    Guid? FournisseurId,
    string? DisplayName,
    string? MatriculeFiscal,
    List<FournisseurMatchCandidateDto> Candidates
);
