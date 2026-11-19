using Einvoicing.Application.DTOs;
using Einvoicing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Einvoicing.Infrastructure.Services;

namespace Einvoicing.Api.Controllers;

[ApiController]
[Route("api/knowledge-base")]
[Produces("application/json")]
[Authorize]
public sealed class KnowledgeBaseController(
    IKnowledgeBaseService knowledgeBase,
    ICurrentUserService currentUser,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    public sealed record ImportKnowledgeDocumentRequest(
        IFormFile File,
        string Title,
        string? Category,
        string? Source,
        DateTime? PublishedAt);

    public sealed record UpdateKnowledgeDocumentStatusRequest(string Status);
    public sealed record UpdateKnowledgeDocumentContentRequest(string Content);
    public sealed record ReindexKnowledgeRequest(bool ResetIndex = false);
    public sealed record AssistantFeedbackRequest(string Question,bool Found,bool Helpful);

    [HttpPost("feedback")]
    [Authorize(Policy = "RagUser")]
    public async Task<IActionResult> Feedback([FromBody] AssistantFeedbackRequest request,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(request.Question)||request.Question.Length>1500)return BadRequest();
        await new KnowledgeFeedbackStore(configuration,environment).RecordAsync(request.Question,request.Found,request.Helpful,ct);
        return NoContent();
    }

    [HttpPost("reindex")]
    [Authorize(Policy = "SuperOuAdmin")]
    public async Task<IActionResult> Reindex([FromBody] ReindexKnowledgeRequest request, CancellationToken ct)
    {
        try { return Ok(new { chunks = await knowledgeBase.ReindexAsync(request.ResetIndex,ct) }); }
        catch (InvalidOperationException e) { return Problem(title:e.Message,statusCode:StatusCodes.Status503ServiceUnavailable); }
    }

    [HttpGet]
    [Authorize(Policy = "SuperOuAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeDocumentDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<KnowledgeDocumentDto>> List(CancellationToken ct)
        => knowledgeBase.ListAsync(ct);

    [HttpGet("{id:guid}/content")]
    [Authorize(Policy = "SuperOuAdmin")]
    [ProducesResponseType(typeof(KnowledgeDocumentContentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken ct)
    {
        try { return Ok(await knowledgeBase.GetContentAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/content")]
    [Authorize(Policy = "SuperOuAdmin")]
    [ProducesResponseType(typeof(KnowledgeDocumentContentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateContent(Guid id, [FromBody] UpdateKnowledgeDocumentContentRequest request, CancellationToken ct)
    {
        if (currentUser.UtilisateurId is not Guid userId)
            return Problem(title: "Utilisateur invalide", statusCode: StatusCodes.Status401Unauthorized);
        try { return Ok(await knowledgeBase.UpdateContentAsync(id, request.Content, userId, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

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
    [Authorize(Policy = "RagUser")]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int take = 5, CancellationToken ct = default)
    {
        try { return Ok(await knowledgeBase.SearchAsync(query,take,ct)); }
        catch (ArgumentException e) { return BadRequest(new {message=e.Message}); }
        catch (InvalidOperationException e) { return Problem(title:e.Message,statusCode:503); }
    }

    [HttpPost("ask")]
    [Authorize(Policy = "RagUser")]
    [ProducesResponseType(typeof(AssistantAnswerDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ask([FromBody] AssistantQuestionDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Saisissez votre question." });
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timeoutSeconds = Math.Clamp(configuration.GetValue("Rag:RequestTimeoutSeconds", 40), 5, 90);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { return Ok(await knowledgeBase.AskAsync(request, deadline.Token)); }
        catch (ArgumentException e) { return BadRequest(new {message=e.Message}); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "La demande a dépassé le délai prévu." });
        }
        catch (InvalidOperationException e) { return Problem(title:e.Message,statusCode:503); }
    }
}
