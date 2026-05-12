namespace Einvoicing.Application.DTOs;

public record ReceiptReviewRequest(
    string? Libelle,
    DateTime? Date,
    decimal? Montant,
    string? TiersNom,
    Guid? FournisseurId,
    string? FournisseurMatriculeFiscal,
    List<ValidateScanFieldRequest> Fields
);

public record CategorizeExpenseAllocationRequest(
    string CategoryName,
    decimal Percentage,
    decimal Amount
);

public record CategorizeExpenseRequest(
    string? CategorieNom,
    List<CategorizeExpenseAllocationRequest> Allocations,
    string? AccountingPeriodLabel,
    decimal? RecoverableVatAmount,
    decimal? RecoverableVatRate
);

public record BankMatchRequest(
    string? Status,
    string? Reference,
    DateTime? Date,
    decimal? Amount,
    string? Counterparty
);

public record AddExpenseCommentRequest(string Message);

public record ApproveExpenseRequest(string? Commentaire);
