using Einvoicing.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace Einvoicing.Application.Interfaces;

public interface ITransactionService
{
    Task<ListeTransactionsDto> ListerAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
    Task<TransactionDto> ObtenirParIdAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);
    Task<TransactionDto> CreerAsync(Guid entrepriseId, Guid utilisateurId, CreerTransactionRequest request, CancellationToken ct = default);
    Task<TransactionDto> MettreAJourAsync(Guid id, Guid entrepriseId, Guid utilisateurId, MettreAJourTransactionRequest request, CancellationToken ct = default);
    Task SupprimerAsync(Guid id, Guid entrepriseId, CancellationToken ct = default);

    Task<TransactionDto> UploadJustificatifAsync(Guid id, Guid entrepriseId, Guid utilisateurId, IFormFile file, CancellationToken ct = default);
    Task<TransactionDto> LierFactureAsync(Guid id, Guid entrepriseId, Guid utilisateurId, Guid factureId, CancellationToken ct = default);

    Task<TransactionCountersDto> CountersAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
    Task<List<TransactionCategorieResumeDto>> ResumeParCategorieAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);

    Task<byte[]> ExporterCsvAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
    Task<byte[]> ExporterPdfAsync(Guid entrepriseId, FiltreTransactionsRequest filtre, CancellationToken ct = default);
}

