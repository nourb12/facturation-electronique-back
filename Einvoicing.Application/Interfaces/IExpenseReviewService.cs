using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IExpenseReviewService
{
    Task<ListeTransactionsDto> ListerPendingReviewAsync(Guid entrepriseId, int page, int parPage, string? recherche, CancellationToken ct = default);
    Task<TransactionDto> SauvegarderJustificatifAsync(Guid transactionId, Guid entrepriseId, Guid utilisateurId, ReceiptReviewRequest request, CancellationToken ct = default);
    Task<TransactionDto> CategoriserAsync(Guid transactionId, Guid entrepriseId, Guid utilisateurId, CategorizeExpenseRequest request, CancellationToken ct = default);
    Task<TransactionDto> EnregistrerRapprochementAsync(Guid transactionId, Guid entrepriseId, Guid utilisateurId, BankMatchRequest request, CancellationToken ct = default);
    Task<TransactionDto> AjouterCommentaireAsync(Guid transactionId, Guid entrepriseId, Guid utilisateurId, AddExpenseCommentRequest request, CancellationToken ct = default);
    Task<TransactionDto> ApprouverAsync(Guid transactionId, Guid entrepriseId, Guid utilisateurId, ApproveExpenseRequest request, CancellationToken ct = default);
}
