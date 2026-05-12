using Einvoicing.Domain.Enums;
using Einvoicing.Domain.Exceptions;

namespace Einvoicing.Domain.Entities;

public sealed class ScannedDocument
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public Guid CreePar { get; private set; }
    public Guid? ModifiePar { get; private set; }

    public string DocumentType { get; private set; } = string.Empty;
    public DocumentSource Source { get; private set; }
    public ScannedDocumentStatus Status { get; private set; }

    public string FilePath { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string? ContentType { get; private set; }
    public long SizeBytes { get; private set; }

    public int OverallConfidence { get; private set; }
    public string RawText { get; private set; } = string.Empty;
    public string FieldsJson { get; private set; } = "[]";
    public string MissingFieldsJson { get; private set; } = "[]";
    public string? ErrorMessage { get; private set; }
    public Guid? TransactionId { get; private set; }

    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }
    public DateTime? ValideLe { get; private set; }

    private ScannedDocument() { }

    public static ScannedDocument Creer(
        Guid entrepriseId,
        Guid creePar,
        string documentType,
        DocumentSource source,
        string filePath,
        string fileName,
        string? contentType,
        long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ValidationMetierException("Le type de document est requis.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ValidationMetierException("Le fichier du scan est requis.");

        var now = DateTime.UtcNow;

        return new ScannedDocument
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            CreePar = creePar,
            DocumentType = documentType.Trim(),
            Source = source,
            Status = ScannedDocumentStatus.Uploaded,
            FilePath = filePath.Trim(),
            FileName = string.IsNullOrWhiteSpace(fileName) ? "scan" : fileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim(),
            SizeBytes = sizeBytes,
            CreeLe = now,
            ModifieLe = now
        };
    }

    public void EnregistrerExtraction(
        Guid modifiePar,
        int overallConfidence,
        string fieldsJson,
        string missingFieldsJson,
        string rawText,
        string? errorMessage)
    {
        ModifiePar = modifiePar;
        OverallConfidence = Math.Clamp(overallConfidence, 0, 100);
        RawText = rawText ?? string.Empty;
        FieldsJson = string.IsNullOrWhiteSpace(fieldsJson) ? "[]" : fieldsJson;
        MissingFieldsJson = string.IsNullOrWhiteSpace(missingFieldsJson) ? "[]" : missingFieldsJson;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        ModifieLe = DateTime.UtcNow;
    }

    public void MarquerCommeReluMobile(
        Guid modifiePar,
        string fieldsJson,
        string missingFieldsJson)
    {
        ModifiePar = modifiePar;
        FieldsJson = string.IsNullOrWhiteSpace(fieldsJson) ? FieldsJson : fieldsJson;
        MissingFieldsJson = string.IsNullOrWhiteSpace(missingFieldsJson) ? MissingFieldsJson : missingFieldsJson;
        Status = ScannedDocumentStatus.MobileReviewed;
        ModifieLe = DateTime.UtcNow;
    }

    public void AssocierTransaction(Guid modifiePar, Guid transactionId)
    {
        ModifiePar = modifiePar;
        TransactionId = transactionId;
        Status = ScannedDocumentStatus.LinkedToTransaction;
        ValideLe = DateTime.UtcNow;
        ModifieLe = DateTime.UtcNow;
    }
}
