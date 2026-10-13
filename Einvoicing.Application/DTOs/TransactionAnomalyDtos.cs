namespace Einvoicing.Application.DTOs;

public sealed record TransactionAnomalyMetricDto(
    string Model,
    decimal Precision,
    decimal Recall,
    decimal F1,
    decimal RocAuc,
    decimal PrAuc,
    decimal PrecisionAtK);

public sealed record TransactionAnomalySummaryDto(
    int TransactionsAnalyzed,
    int SuspectedTransactions,
    int NormalTransactions,
    string SelectedModel);

public sealed record TransactionAnomalyItemDto(
    string TransactionId,
    decimal Time,
    decimal Amount,
    decimal AnomalyScore,
    bool AnomalyDetected,
    string Model,
    DateTime? Date = null,
    string? Label = null,
    string? Counterparty = null,
    string? Reason = null);

public sealed record TransactionAnomalyPcaPointDto(
    string TransactionId,
    decimal PcaX,
    decimal PcaY,
    decimal AnomalyScore,
    bool AnomalyDetected);

public sealed record TransactionAnomalyReportDto(
    TransactionAnomalySummaryDto Summary,
    string SelectedModel,
    IReadOnlyList<TransactionAnomalyMetricDto> Models,
    IReadOnlyList<TransactionAnomalyItemDto> TopSuspicious,
    IReadOnlyList<TransactionAnomalyPcaPointDto> PcaPoints,
    string Source = "transactions_tuniflow",
    string? ModelDescription = null,
    string? ReviewNote = null);

public sealed record TransactionAnomalyScoreRequest(
    double Time,
    double Amount,
    IReadOnlyList<double> Components);

public sealed record TransactionAnomalyScoreDto(
    string Model,
    decimal AnomalyScore,
    string Classification,
    bool RequiresReview);
