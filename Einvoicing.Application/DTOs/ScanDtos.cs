using System.Text.Json.Serialization;

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
    string SchemaVersion,
    [property: JsonPropertyName("schema_version")] string SchemaVersionSnake,
    Guid ScannedDocumentId,
    string DocumentType,
    int OverallConfidence,
    string ImageUrl,
    List<ExtractedFieldDto> Fields,
    List<string> MissingFields,
    bool RequiresManualReview,
    Dictionary<string, object>? Document,
    string RawText,
    string SourceMode,
    Dictionary<string, object>? ImageQuality,
    string? ErrorMessage
);

public record ValidateScanFieldRequest(
    string Key,
    string? Value,
    string? Label = null,
    int? Confidence = null,
    bool? Required = null
);

public record ValidateScanRequest(
    Guid ScannedDocumentId,
    List<ValidateScanFieldRequest> Fields,
    string? DocumentType = null,
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
    public string SchemaVersion { get; set; } = "1.0";
    [JsonPropertyName("schema_version")]
    public string? SchemaVersionSnake { get; set; }
    public bool OcrSuccess { get; set; }
    public int OverallConfidence { get; set; }
    public string DocumentType { get; set; } = "facture";
    public List<ExtractedFieldDto> Fields { get; set; } = [];
    public List<string> MissingFields { get; set; } = [];
    public Dictionary<string, object>? Document { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string SourceMode { get; set; } = "ocr";
    public Dictionary<string, object>? ImageQuality { get; set; }
    public string? ErrorMessage { get; set; }
}
