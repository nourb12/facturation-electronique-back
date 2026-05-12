using System.Net.Http.Headers;
using System.Text.Json;
using Einvoicing.Application.Interfaces;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Einvoicing.Infrastructure.Services;

public sealed class OcrClient(
    HttpClient http,
    IConfiguration config,
    ILogger<OcrClient> logger) : IOcrClient
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    public async Task<OcrExtractionResult?> ExtractAsync(
        IEnumerable<IFormFile?> files,
        CancellationToken ct = default)
    {
        var baseUrl = config["OcrService:BaseUrl"];

        // ── Service non configuré → scoring sans OCR (dégradation gracieuse) ─
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogInformation("OcrService:BaseUrl non configuré — scoring sans OCR.");
            return new OcrExtractionResult
            {
                OcrSuccess = false,
                ConfidenceScore = 0d,
                ErreurMessage = "Service OCR non configuré. Validation manuelle requise.",
                TexteBrut = string.Empty
            };
        }

        var list = files?.Where(f => f is not null).ToList() ?? [];
        if (list.Count == 0)
            return new OcrExtractionResult
            {
                OcrSuccess = false,
                ConfidenceScore = 0d,
                ErreurMessage = "Aucun fichier transmis au service OCR.",
                TexteBrut = string.Empty
            };

        try
        {
            using var form = new MultipartFormDataContent();
            foreach (var file in list)
            {
                var streamContent = new StreamContent(file!.OpenReadStream());
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                form.Add(streamContent, "files", file.FileName);
            }

            using var resp = await http.PostAsync(
                $"{baseUrl.TrimEnd('/')}/ocr/parse", form, ct);

            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("OCR service responded {Status}: {Body}",
                    (int)resp.StatusCode, json[..Math.Min(200, json.Length)]);

                return new OcrExtractionResult
                {
                    OcrSuccess = false,
                    ConfidenceScore = 0d,
                    ErreurMessage = $"OCR service {(int)resp.StatusCode} : {json[..Math.Min(300, json.Length)]}",
                    TexteBrut = json
                };
            }

            var result = JsonSerializer.Deserialize<OcrExtractionResult>(json, JsonOpts);
            return result ?? new OcrExtractionResult
            {
                OcrSuccess = false,
                ConfidenceScore = 0d,
                ErreurMessage = "Réponse OCR vide ou invalide.",
                TexteBrut = json
            };
        }
        catch (HttpRequestException ex)
        {
            // Service OCR inaccessible (pas démarré, port fermé…)
            logger.LogWarning(ex, "OCR service inaccessible ({Url})", baseUrl);
            return new OcrExtractionResult
            {
                OcrSuccess = false,
                ConfidenceScore = 0d,
                ErreurMessage = $"OCR service inaccessible : {ex.Message}",
                TexteBrut = string.Empty
            };
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("OCR service timeout");
            return new OcrExtractionResult
            {
                OcrSuccess = false,
                ConfidenceScore = 0d,
                ErreurMessage = "Timeout — le service OCR n'a pas répondu à temps.",
                TexteBrut = string.Empty
            };
        }
    }

    public async Task<AccountingOcrResult?> ExtractAccountingAsync(
        IFormFile file,
        string documentType,
        CancellationToken ct = default)
    {
        var baseUrl = config["OcrService:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogInformation("OcrService:BaseUrl non configuré — OCR comptable indisponible.");
            return new AccountingOcrResult
            {
                OcrSuccess = false,
                DocumentType = documentType,
                OverallConfidence = 0,
                ErrorMessage = "Service OCR non configuré."
            };
        }

        try
        {
            using var form = new MultipartFormDataContent();
            var streamContent = new StreamContent(file.OpenReadStream());
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            form.Add(streamContent, "file", file.FileName);
            form.Add(new StringContent(documentType), "document_type");

            using var resp = await http.PostAsync($"{baseUrl.TrimEnd('/')}/ocr/accounting/parse", form, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Accounting OCR responded {Status}: {Body}",
                    (int)resp.StatusCode, json[..Math.Min(200, json.Length)]);

                return new AccountingOcrResult
                {
                    OcrSuccess = false,
                    DocumentType = documentType,
                    OverallConfidence = 0,
                    ErrorMessage = $"OCR comptable {(int)resp.StatusCode} : {json[..Math.Min(300, json.Length)]}"
                };
            }

            var result = JsonSerializer.Deserialize<AccountingOcrResult>(json, JsonOpts);
            return result ?? new AccountingOcrResult
            {
                OcrSuccess = false,
                DocumentType = documentType,
                OverallConfidence = 0,
                ErrorMessage = "Réponse OCR comptable vide ou invalide."
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Accounting OCR inaccessible ({Url})", baseUrl);
            return new AccountingOcrResult
            {
                OcrSuccess = false,
                DocumentType = documentType,
                OverallConfidence = 0,
                ErrorMessage = $"OCR comptable inaccessible : {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Accounting OCR timeout");
            return new AccountingOcrResult
            {
                OcrSuccess = false,
                DocumentType = documentType,
                OverallConfidence = 0,
                ErrorMessage = "Timeout — le service OCR comptable n'a pas répondu à temps."
            };
        }
    }
}
