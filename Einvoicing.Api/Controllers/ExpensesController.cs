using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Produces("application/json")]
[Authorize]
public sealed class ExpensesController(
    IExpenseReviewService expenseReviewService,
    ITransactionService transactionService,
    ICurrentUserService currentUser) : ControllerBase
{
    private IActionResult? EntrepriseRequise(out Guid entrepriseId, out Guid utilisateurId)
    {
        entrepriseId = Guid.Empty;
        utilisateurId = Guid.Empty;

        if (currentUser.EntrepriseId is null)
            return Problem(
                title: "Entreprise non rattachée",
                detail: "Votre compte n'est pas encore rattaché à une entreprise.",
                statusCode: 403);

        if (currentUser.UtilisateurId is null)
            return Problem(title: "Utilisateur invalide", statusCode: 401);

        entrepriseId = currentUser.EntrepriseId.Value;
        utilisateurId = currentUser.UtilisateurId.Value;
        return null;
    }

    [HttpGet("pending-review")]
    [ProducesResponseType(typeof(ListeTransactionsDto), 200)]
    public async Task<IActionResult> PendingReview(
        [FromQuery] int page = 1,
        [FromQuery] int parPage = 20,
        [FromQuery] string? recherche = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out _);
        if (guard is not null) return guard;

        var result = await expenseReviewService.ListerPendingReviewAsync(entrepriseId, page, parPage, recherche, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out _);
        if (guard is not null) return guard;

        var result = await transactionService.ObtenirParIdAsync(id, entrepriseId, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/receipt-review")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> SaveReceiptReview(Guid id, [FromBody] ReceiptReviewRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var result = await expenseReviewService.SauvegarderJustificatifAsync(id, entrepriseId, utilisateurId, request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/categorize")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> Categorize(Guid id, [FromBody] CategorizeExpenseRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var result = await expenseReviewService.CategoriserAsync(id, entrepriseId, utilisateurId, request, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddExpenseCommentRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var result = await expenseReviewService.AjouterCommentaireAsync(id, entrepriseId, utilisateurId, request, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/match-bank")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> MatchBank(Guid id, [FromBody] BankMatchRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var result = await expenseReviewService.EnregistrerRapprochementAsync(id, entrepriseId, utilisateurId, request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveExpenseRequest request, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var entrepriseId, out var utilisateurId);
        if (guard is not null) return guard;

        var result = await expenseReviewService.ApprouverAsync(id, entrepriseId, utilisateurId, request, ct);
        return Ok(result);
    }
}
