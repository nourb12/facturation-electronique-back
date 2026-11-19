using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Interfaces;

public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<KnowledgeDocumentDto>> ListAsync(CancellationToken ct = default);
    Task<KnowledgeDocumentContentDto> GetContentAsync(Guid id, CancellationToken ct = default);
    Task<KnowledgeDocumentContentDto> UpdateContentAsync(Guid id, string content, Guid userId, CancellationToken ct = default);
    Task<KnowledgeDocumentDto> ImportAsync(Guid userId, string title, string category, string source, DateTime? publishedAt, Stream content, string fileName, string mimeType, CancellationToken ct = default);
    Task SetStatusAsync(Guid id, string status, Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeSearchResultDto>> SearchAsync(string query, int take = 5, CancellationToken ct = default);
    Task<AssistantAnswerDto> AskAsync(string question, CancellationToken ct = default);
    Task<AssistantAnswerDto> AskAsync(AssistantQuestionDto request, CancellationToken ct = default);
    Task<int> ReindexAsync(bool resetIndex, CancellationToken ct = default);
}
