using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface ITransactionAnomalyService
{
    Task<TransactionAnomalyReportDto> GetReportAsync(Guid entrepriseId, CancellationToken ct = default);
    Task<TransactionAnomalyScoreDto> ScoreAsync(TransactionAnomalyScoreRequest request, CancellationToken ct = default);
}
