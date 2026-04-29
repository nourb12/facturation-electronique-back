using Einvoicing.Application.DTOs;
using Einvoicing.Domain.Entities;

namespace Einvoicing.Application.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> ObtenirParIdAsync(Guid id, CancellationToken ct = default);
    Task<(List<Transaction> Items, int Total)> ListerAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
    Task<List<Transaction>> ListerTousAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
    Task<TransactionCountersDto> CountersAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
    Task<List<TransactionCategorieResumeDto>> ResumeParCategorieAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
    Task AjouterAsync(Transaction transaction, CancellationToken ct = default);
    void MettreAJour(Transaction transaction);
    void Supprimer(Transaction transaction);
    Task SauvegarderAsync(CancellationToken ct = default);
}

