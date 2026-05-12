using Einvoicing.Domain.Enums;

namespace Einvoicing.Application.DTOs;

public record FiltreTransactionsRequest(
    int Page = 1,
    int ParPage = 20,
    StatutTransaction? Statut = null,
    TypeTransaction? Type = null,
    DateTime? DateDebut = null,
    DateTime? DateFin = null,
    string? CategorieNom = null,
    string? Recherche = null,
    DocumentSource? Source = null
);

public record CreerTransactionRequest(
    DateTime Date,
    string Libelle,
    decimal Montant,
    TypeTransaction Type,
    string Devise = "TND",
    string? TiersNom = null,
    string? CategorieNom = null,
    string? Description = null,
    string? Compte = null,
    Guid? FactureId = null
);

public record MettreAJourTransactionRequest(
    DateTime? Date = null,
    string? Libelle = null,
    decimal? Montant = null,
    TypeTransaction? Type = null,
    string? Devise = null,
    string? TiersNom = null,
    string? CategorieNom = null,
    string? Description = null,
    string? Compte = null,
    Guid? FactureId = null,
    StatutTransaction? Statut = null,
    StatutJustificatif? StatutJustificatif = null
);

public record TransactionDocumentDto(
    string FileName,
    string Url,
    string? ContentType,
    long? SizeBytes
);

public record TransactionAllocationDto(
    Guid Id,
    string CategoryName,
    decimal Percentage,
    decimal Amount
);

public record TransactionCommentDto(
    Guid Id,
    Guid? AuthorId,
    string AuthorName,
    string Message,
    DateTime CreatedAt
);

public record TransactionActivityDto(
    Guid Id,
    Guid? AuthorId,
    string? AuthorName,
    string Action,
    string Description,
    DateTime CreatedAt
);

public record TransactionBankSuggestionDto(
    string Label,
    string MatchType,
    DateTime Date,
    decimal Amount,
    string? Counterparty
);

public record TransactionBankMatchDto(
    string? Status,
    string? Reference,
    DateTime? Date,
    decimal? Amount,
    string? Counterparty,
    List<TransactionBankSuggestionDto> Suggestions
);

public record TransactionDto(
    Guid Id,
    Guid EntrepriseId,
    DateTime Date,
    string Libelle,
    string? Description,
    string? TiersNom,
    string? CategorieNom,
    string Source,
    Guid? FournisseurId,
    string? FournisseurMatriculeFiscal,
    string? DocumentType,
    int? OcrOverallConfidence,
    TypeTransaction Type,
    StatutTransaction Statut,
    StatutJustificatif? StatutJustificatif,
    decimal Montant,
    string Devise,
    string? Compte,
    Guid? FactureId,
    TransactionDocumentDto? DocumentLie,
    List<ExtractedFieldDto> ReviewFields,
    List<string> MissingFieldKeys,
    List<TransactionAllocationDto> Allocations,
    List<TransactionCommentDto> Comments,
    List<TransactionActivityDto> Activities,
    TransactionBankMatchDto? BankMatch,
    string? AccountingPeriodLabel,
    decimal? RecoverableVatAmount,
    decimal? RecoverableVatRate,
    DateTime CreeLe,
    DateTime ModifieLe
);

public record ListeTransactionsDto(
    List<TransactionDto> Items,
    int Total,
    int Page,
    int ParPage
);

public record TransactionCountersDto(
    int NonJustifieeCount,
    decimal NonJustifieeMontant,
    int EnAttenteCount,
    decimal EnAttenteMontant,
    int JustifieeCount,
    decimal JustifieeMontant
);

public record TransactionCategorieResumeDto(
    string CategorieId,
    string CategorieNom,
    TypeTransaction Type,
    int Count,
    decimal Montant
);
