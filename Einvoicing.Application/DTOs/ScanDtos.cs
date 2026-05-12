namespace Einvoicing.Application.DTOs;

public record ExtractedFieldDto(
    string Key,
    string Label,
    string? Value,
    int Confidence,
    bool Required,
    bool RequiresReview
);

public record ScanUploadResponseDto(
    Guid ScannedDocumentId,
    string DocumentType,
    int OverallConfidence,
    string ImageUrl,
    List<ExtractedFieldDto> Fields,
    List<string> MissingFields,
    bool RequiresManualReview,
    string RawText,
    string? ErrorMessage
);

public record ValidateScanFieldRequest(
    string Key,
    string? Value
);

public record ValidateScanRequest(
    Guid ScannedDocumentId,
    List<ValidateScanFieldRequest> Fields,
    Guid? SupplierId = null,
    string? SupplierName = null,
    string? SupplierTaxId = null,
    string? SupplierAddress = null,
    string? SupplierIban = null
);

public record ValidateScanResponseDto(
    Guid ScannedDocumentId,
    string DocumentType,
    string ReviewStatus,
    Guid? TransactionId,
    string? Message
);

public sealed class AccountingOcrResult
{
    public bool OcrSuccess { get; set; }
    public int OverallConfidence { get; set; }
    public string DocumentType { get; set; } = "supplier_invoice";
    public List<ExtractedFieldDto> Fields { get; set; } = [];
    public List<string> MissingFields { get; set; } = [];
    public string RawText { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
