using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Einvoicing.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Produces("application/json")]
[Authorize]
public sealed class TransactionsController(
    ITransactionService transactionService,
    ICurrentUserService currentUser
) : ControllerBase
{
    private Guid? EntrepriseIdOpt => currentUser.EntrepriseId;
    private Guid? UtilisateurIdOpt => currentUser.UtilisateurId;

    private IActionResult? EntrepriseRequise(out Guid entrepriseId, out Guid utilisateurId)
    {
        entrepriseId = Guid.Empty;
        utilisateurId = Guid.Empty;

        if (EntrepriseIdOpt is null)
            return Problem(
                title: "Entreprise non rattachée",
                detail: "Votre compte n'est pas encore rattaché à une entreprise.",
                statusCode: 403);

        if (UtilisateurIdOpt is null)
            return Problem(title: "Utilisateur invalide", statusCode: 401);

        entrepriseId = EntrepriseIdOpt.Value;
        utilisateurId = UtilisateurIdOpt.Value;
        return null;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListeTransactionsDto), 200)]
    public async Task<IActionResult> Lister(
        [FromQuery] int page = 1,
        [FromQuery] int parPage = 20,
        [FromQuery] StatutTransaction? statut = null,
        [FromQuery] TypeTransaction? type = null,
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        [FromQuery] string? categorieNom = null,
        [FromQuery] string? recherche = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var filtre = new FiltreTransactionsRequest(page, parPage, statut, type, dateDebut, dateFin, categorieNom, recherche);
        var result = await transactionService.ListerAsync(eId, filtre, ct);
        return Ok(result);
    }

    [HttpGet("counters")]
    [ProducesResponseType(typeof(TransactionCountersDto), 200)]
    public async Task<IActionResult> Counters(
        [FromQuery] StatutTransaction? statut = null,
        [FromQuery] TypeTransaction? type = null,
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        [FromQuery] string? categorieNom = null,
        [FromQuery] string? recherche = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var filtre = new FiltreTransactionsRequest(1, 1, statut, type, dateDebut, dateFin, categorieNom, recherche);
        var result = await transactionService.CountersAsync(eId, filtre, ct);
        return Ok(result);
    }

    [HttpGet("summary/categories")]
    [ProducesResponseType(typeof(List<TransactionCategorieResumeDto>), 200)]
    public async Task<IActionResult> ResumeParCategorie(
        [FromQuery] StatutTransaction? statut = null,
        [FromQuery] TypeTransaction? type = null,
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        [FromQuery] string? categorieNom = null,
        [FromQuery] string? recherche = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var filtre = new FiltreTransactionsRequest(1, 1, statut, type, dateDebut, dateFin, categorieNom, recherche);
        var result = await transactionService.ResumeParCategorieAsync(eId, filtre, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> ObtenirParId(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var result = await transactionService.ObtenirParIdAsync(id, eId, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 201)]
    public async Task<IActionResult> Creer([FromBody] CreerTransactionRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await transactionService.CreerAsync(eId, uId, req, ct);
        return CreatedAtAction(nameof(ObtenirParId), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> MettreAJour(Guid id, [FromBody] MettreAJourTransactionRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await transactionService.MettreAJourAsync(id, eId, uId, req, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Supprimer(Guid id, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        await transactionService.SupprimerAsync(id, eId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/receipt")]
    [Authorize(Policy = "Tous")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> UploadJustificatif(Guid id, IFormFile file, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await transactionService.UploadJustificatifAsync(id, eId, uId, file, ct);
        return Ok(result);
    }

    public record LierFactureRequest(Guid InvoiceId);

    [HttpPost("{id:guid}/link-invoice")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    public async Task<IActionResult> LierFacture(Guid id, [FromBody] LierFactureRequest req, CancellationToken ct)
    {
        var guard = EntrepriseRequise(out var eId, out var uId);
        if (guard is not null) return guard;

        var result = await transactionService.LierFactureAsync(id, eId, uId, req.InvoiceId, ct);
        return Ok(result);
    }

    [HttpGet("export/csv")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] StatutTransaction? statut = null,
        [FromQuery] TypeTransaction? type = null,
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        [FromQuery] string? categorieNom = null,
        [FromQuery] string? recherche = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var filtre = new FiltreTransactionsRequest(1, int.MaxValue, statut, type, dateDebut, dateFin, categorieNom, recherche);
        var bytes = await transactionService.ExporterCsvAsync(eId, filtre, ct);
        return File(bytes, "text/csv; charset=utf-8", "transactions.csv");
    }

    [HttpGet("export/pdf")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] StatutTransaction? statut = null,
        [FromQuery] TypeTransaction? type = null,
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null,
        [FromQuery] string? categorieNom = null,
        [FromQuery] string? recherche = null,
        CancellationToken ct = default)
    {
        var guard = EntrepriseRequise(out var eId, out _);
        if (guard is not null) return guard;

        var filtre = new FiltreTransactionsRequest(1, int.MaxValue, statut, type, dateDebut, dateFin, categorieNom, recherche);
        var bytes = await transactionService.ExporterPdfAsync(eId, filtre, ct);
        return File(bytes, "application/pdf", "journal-transactions.pdf");
    }
}
