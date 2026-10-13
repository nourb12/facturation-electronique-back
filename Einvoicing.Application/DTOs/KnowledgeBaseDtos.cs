namespace Einvoicing.Application.DTOs;

public sealed record KnowledgeDocumentDto(
    Guid Id,
    string Title,
    string Category,
    string Source,
    string Status,
    string OriginalFileName,
    DateTime? PublishedAt,
    DateTime? ValidatedAt,
    int ChunkCount,
    string? ExtractionError,
    DateTime CreatedAt);

public sealed record KnowledgeSearchResultDto(
    Guid DocumentId,
    string DocumentTitle,
    string Category,
    string Source,
    int Position,
    string Heading,
    string Content,
    int Score);

public sealed record AssistantQuestionDto(string Question);

public sealed record AssistantSourceDto(Guid DocumentId, string Title, string Category, string Source, string Heading);

public sealed record AssistantAnswerDto(string Answer, bool Found, string Mode, IReadOnlyList<AssistantSourceDto> Sources);
