namespace Einvoicing.Domain.Entities;

public sealed class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EntrepriseId { get; set; }
    public Guid CreatedById { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Guide produit";
    public string Source { get; set; } = "Interne";
    public string Status { get; set; } = "Brouillon";
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ExtractedText { get; set; }
    public string? ExtractionError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<KnowledgeChunk> Chunks { get; set; } = [];
}

public sealed class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int Position { get; set; }
    public string Heading { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public KnowledgeDocument? Document { get; set; }
}
