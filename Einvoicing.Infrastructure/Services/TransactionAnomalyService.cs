using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;

namespace Einvoicing.Infrastructure.Services;

public sealed class TransactionAnomalyService(
    IHttpClientFactory httpClientFactory,
    ITransactionRepository transactionRepo) : ITransactionAnomalyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TransactionAnomalyReportDto> GetReportAsync(Guid entrepriseId, CancellationToken ct = default)
    {
        var transactions = await transactionRepo.ListerTousAsync(
            entrepriseId,
            new FiltreTransactionsRequest(Page: 1, ParPage: 5000),
            ct);

        if (transactions.Count < 6)
            throw new InvalidOperationException("Au moins 6 transactions réelles sont nécessaires pour analyser les habitudes de l’entreprise.");

        var http = httpClientFactory.CreateClient("MachineLearning");
        using var request = new HttpRequestMessage(HttpMethod.Post, "anomaly/company-report")
        {
            Content = JsonContent.Create(new
            {
                transactions = transactions.Select(transaction => new
                {
                    transaction_id = transaction.Id.ToString(),
                    date = transaction.Date,
                    amount = transaction.Montant,
                    label = transaction.Libelle,
                    counterparty = transaction.TiersNom,
                    transaction_type = transaction.Type.ToString(),
                    source = transaction.Source.ToString(),
                    has_document = !string.IsNullOrWhiteSpace(transaction.JustificatifChemin),
                    ocr_confidence = transaction.OcrOverallConfidence,
                }),
            }, options: JsonOptions)
        };
        using var httpResponse = await http.SendAsync(request, ct);
        httpResponse.EnsureSuccessStatusCode();
        var response = await httpResponse.Content.ReadFromJsonAsync<CompanyAnomalyReportResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Le service ML a envoyé un rapport d'anomalies vide.");

        return new TransactionAnomalyReportDto(
            new TransactionAnomalySummaryDto(
                response.Summary.TransactionsAnalyzed,
                response.Summary.SuspectedTransactions,
                response.Summary.NormalTransactions,
                response.Summary.SelectedModel),
            response.SelectedModel,
            [],
            response.TopSuspicious.Select(item => new TransactionAnomalyItemDto(
                item.TransactionId,
                0,
                item.Amount,
                item.AnomalyScore,
                item.AnomalyDetected,
                response.SelectedModel,
                item.Date,
                item.Label,
                item.Counterparty,
                item.Reason)).ToList(),
            response.PcaPoints.Select(point => new TransactionAnomalyPcaPointDto(
                point.TransactionId,
                point.PcaX,
                point.PcaY,
                point.AnomalyScore,
                point.AnomalyDetected)).ToList(),
            response.Source,
            response.ModelDescription,
            response.ReviewNote);
    }

    public async Task<TransactionAnomalyScoreDto> ScoreAsync(TransactionAnomalyScoreRequest request, CancellationToken ct = default)
    {
        if (request.Components.Count != 28)
            throw new ArgumentException("Une transaction ULB doit contenir exactement 28 composantes anonymisées.", nameof(request));

        var http = httpClientFactory.CreateClient("MachineLearning");
        using var response = await http.PostAsJsonAsync("predict/transaction-anomaly", new
        {
            time = request.Time,
            amount = request.Amount,
            components = request.Components,
        }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AnomalyScoreResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Le service ML a envoyé un score d'anomalie vide.");

        return new TransactionAnomalyScoreDto(
            result.Model,
            result.AnomalyScore,
            result.Classification,
            result.RequiresReview);
    }

    private sealed record AnomalyReportResponse(
        MlMetrics Metrics,
        MlSummary Summary,
        [property: JsonPropertyName("top_suspicious")] List<MlItem> TopSuspicious,
        [property: JsonPropertyName("pca_points")] List<MlPoint> PcaPoints);

    private sealed record MlMetrics([property: JsonPropertyName("selected_model")] string SelectedModel, List<MlMetric> Models);
    private sealed record MlMetric(
        string Model,
        decimal Precision,
        decimal Recall,
        decimal F1,
        [property: JsonPropertyName("roc_auc")] decimal RocAuc,
        [property: JsonPropertyName("pr_auc")] decimal PrAuc,
        [property: JsonPropertyName("precision_at_k")] decimal PrecisionAtK);
    private sealed record MlSummary(
        [property: JsonPropertyName("transactions_analyzed")] int TransactionsAnalyzed,
        [property: JsonPropertyName("suspected_transactions")] int SuspectedTransactions,
        [property: JsonPropertyName("normal_transactions")] int NormalTransactions,
        [property: JsonPropertyName("selected_model")] string SelectedModel);
    private sealed record MlItem(
        [property: JsonPropertyName("transaction_id")] string TransactionId,
        decimal Time,
        decimal Amount,
        [property: JsonPropertyName("anomaly_score")] decimal AnomalyScore,
        // Le service Python sérialise ce champ sous forme 0/1.
        [property: JsonPropertyName("anomaly_detected")] int AnomalyDetected,
        string Model);
    private sealed record MlPoint(
        [property: JsonPropertyName("transaction_id")] string TransactionId,
        [property: JsonPropertyName("pca_x")] decimal PcaX,
        [property: JsonPropertyName("pca_y")] decimal PcaY,
        [property: JsonPropertyName("anomaly_score")] decimal AnomalyScore,
        [property: JsonPropertyName("anomaly_detected")] bool AnomalyDetected);
    private sealed record AnomalyScoreResponse(
        string Model,
        [property: JsonPropertyName("anomaly_score")] decimal AnomalyScore,
        string Classification,
        [property: JsonPropertyName("requires_review")] bool RequiresReview);

    private sealed record CompanyAnomalyReportResponse(
        string Source,
        [property: JsonPropertyName("selected_model")] string SelectedModel,
        [property: JsonPropertyName("model_description")] string ModelDescription,
        CompanyMlSummary Summary,
        [property: JsonPropertyName("top_suspicious")] List<CompanyMlItem> TopSuspicious,
        [property: JsonPropertyName("pca_points")] List<CompanyMlPoint> PcaPoints,
        [property: JsonPropertyName("review_note")] string ReviewNote);
    private sealed record CompanyMlSummary(
        [property: JsonPropertyName("transactions_analyzed")] int TransactionsAnalyzed,
        [property: JsonPropertyName("suspected_transactions")] int SuspectedTransactions,
        [property: JsonPropertyName("normal_transactions")] int NormalTransactions,
        [property: JsonPropertyName("selected_model")] string SelectedModel);
    private sealed record CompanyMlItem(
        [property: JsonPropertyName("transaction_id")] string TransactionId,
        DateTime Date,
        decimal Amount,
        string Label,
        string? Counterparty,
        [property: JsonPropertyName("anomaly_score")] decimal AnomalyScore,
        [property: JsonPropertyName("anomaly_detected")] bool AnomalyDetected,
        string Reason);
    private sealed record CompanyMlPoint(
        [property: JsonPropertyName("transaction_id")] string TransactionId,
        [property: JsonPropertyName("pca_x")] decimal PcaX,
        [property: JsonPropertyName("pca_y")] decimal PcaY,
        [property: JsonPropertyName("anomaly_score")] decimal AnomalyScore,
        [property: JsonPropertyName("anomaly_detected")] bool AnomalyDetected);
}
