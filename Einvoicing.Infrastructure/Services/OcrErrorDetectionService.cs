using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;

namespace Einvoicing.Infrastructure.Services;

public sealed class OcrErrorDetectionService(IHttpClientFactory httpClientFactory) : IOcrErrorDetectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<OcrErrorScoreDto> ScoreAsync(OcrErrorScoreRequest request, CancellationToken ct = default)
    {
        if (request.Fields is null || request.Fields.Count == 0)
            throw new ArgumentException("Au moins un champ OCR est requis.", nameof(request));

        var http = httpClientFactory.CreateClient("MachineLearning");
        using var response = await http.PostAsJsonAsync("predict/ocr-error", new
        {
            document_type = request.DocumentType,
            fields = request.Fields.Select(field => new
            {
                key = field.Key,
                label = field.Label,
                value = field.Value,
                confidence = field.Confidence,
                required = field.Required
            })
        }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OcrScoreResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Le service ML a renvoyé une analyse OCR vide.");

        return new OcrErrorScoreDto(
            result.Model,
            result.ErrorProbability,
            result.GlobalStatus,
            result.RequiresReview,
            result.FieldsToReview,
            result.FieldAssessments.Select(field => new OcrFieldAssessmentDto(
                field.Key, field.Label, field.Value, field.Confidence, field.ErrorDetected, field.Status)).ToList(),
            new OcrErrorSummaryDto(result.Summary.CharacterErrors, result.Summary.MissingFields,
                result.Summary.CalculationInconsistencies, result.Summary.InvalidDates));
    }

    private sealed record OcrScoreResponse(
        string Model,
        [property: JsonPropertyName("error_probability")] decimal ErrorProbability,
        [property: JsonPropertyName("global_status")] string GlobalStatus,
        [property: JsonPropertyName("requires_review")] bool RequiresReview,
        [property: JsonPropertyName("fields_to_review")] int FieldsToReview,
        [property: JsonPropertyName("field_assessments")] List<OcrFieldResponse> FieldAssessments,
        OcrSummaryResponse Summary);
    private sealed record OcrFieldResponse(
        string Key,
        string Label,
        string Value,
        decimal Confidence,
        [property: JsonPropertyName("error_detected")] string ErrorDetected,
        string Status);
    private sealed record OcrSummaryResponse(
        [property: JsonPropertyName("character_errors")] int CharacterErrors,
        [property: JsonPropertyName("missing_fields")] int MissingFields,
        [property: JsonPropertyName("calculation_inconsistencies")] int CalculationInconsistencies,
        [property: JsonPropertyName("invalid_dates")] int InvalidDates);
}
