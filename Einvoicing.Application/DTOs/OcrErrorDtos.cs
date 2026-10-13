namespace Einvoicing.Application.DTOs;

public record OcrErrorFieldRequest(string Key, string? Label, string? Value, decimal Confidence, bool Required);
public record OcrErrorScoreRequest(string DocumentType, List<OcrErrorFieldRequest> Fields);
public record OcrFieldAssessmentDto(string Key, string Label, string Value, decimal Confidence, string ErrorDetected, string Status);
public record OcrErrorSummaryDto(int CharacterErrors, int MissingFields, int CalculationInconsistencies, int InvalidDates);
public record OcrErrorScoreDto(
    string Model,
    decimal ErrorProbability,
    string GlobalStatus,
    bool RequiresReview,
    int FieldsToReview,
    List<OcrFieldAssessmentDto> FieldAssessments,
    OcrErrorSummaryDto Summary);
