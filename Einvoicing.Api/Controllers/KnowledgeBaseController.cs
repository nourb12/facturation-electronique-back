using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/knowledge-base")]
[Produces("application/json")]
[Authorize]
public sealed class KnowledgeBaseController(
    IKnowledgeBaseService knowledgeBase,
    ICurrentUserService currentUser) : ControllerBase
{
    public sealed record ImportKnowledgeDocumentRequest(
        IFormFile File,
        string Title,
        string? Category,
        string? Source,
        DateTime? PublishedAt);

    public sealed record UpdateKnowledgeDocumentStatusRequest(string Status);

    [HttpGet]
    [Authorize(Policy = "SuperOuAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeDocumentDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<KnowledgeDocumentDto>> List(CancellationToken ct)
        => knowledgeBase.ListAsync(ct);

    [HttpPost("import")]
    [Authorize(Policy = "SuperOuAdmin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(KnowledgeDocumentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import([FromForm] ImportKnowledgeDocumentRequest request, CancellationToken ct)
    {
        if (currentUser.UtilisateurId is not Guid userId)
            return Problem(title: "Utilisateur invalide", statusCode: StatusCodes.Status401Unauthorized);
        if (request.File is null || request.File.Length == 0)
            return BadRequest(new { message = "Sélectionnez un document à importer." });

        try
        {
            await using var content = request.File.OpenReadStream();
            var result = await knowledgeBase.ImportAsync(
                userId,
                request.Title,
                request.Category ?? "Guide produit",
                request.Source ?? "Interne",
                request.PublishedAt,
                content,
                request.File.FileName,
                request.File.ContentType,
                ct);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "SuperOuAdmin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateKnowledgeDocumentStatusRequest request, CancellationToken ct)
    {
        if (currentUser.UtilisateurId is not Guid userId)
            return Problem(title: "Utilisateur invalide", statusCode: StatusCodes.Status401Unauthorized);

        try
        {
            await knowledgeBase.SetStatusAsync(id, request.Status, userId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SuperOuAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await knowledgeBase.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("search")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeSearchResultDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<KnowledgeSearchResultDto>> Search([FromQuery] string query, [FromQuery] int take = 5, CancellationToken ct = default)
        => knowledgeBase.SearchAsync(query, take, ct);

    [HttpPost("ask")]
    [Authorize(Policy = "Tous")]
    [ProducesResponseType(typeof(AssistantAnswerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ask([FromBody] AssistantQuestionDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Saisissez votre question." });
        return Ok(await knowledgeBase.AskAsync(request.Question, ct));
    }
}
