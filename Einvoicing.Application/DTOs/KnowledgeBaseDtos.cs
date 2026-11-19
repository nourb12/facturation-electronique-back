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

public sealed record KnowledgeDocumentContentDto(Guid Id, string Title, string Content, string Status, DateTime UpdatedAt);

public sealed record KnowledgeSearchResultDto(
    Guid DocumentId,
    string DocumentTitle,
    string Category,
    string Source,
    int Position,
    string Heading,
    string Content,
    int Score,
    double? Similarity = null);

public sealed record AssistantHistoryMessageDto(string Role, string Text);
public sealed record AssistantQuestionDto(string Question, IReadOnlyList<AssistantHistoryMessageDto>? History = null);

public sealed record AssistantSourceDto(Guid DocumentId, string Title, string Category, string Source, string Heading, string? Excerpt = null);

public sealed record AssistantAnswerDto(string Answer, bool Found, string Mode, IReadOnlyList<AssistantSourceDto> Sources, IReadOnlyList<string>? Suggestions = null, string? Warning = null);
