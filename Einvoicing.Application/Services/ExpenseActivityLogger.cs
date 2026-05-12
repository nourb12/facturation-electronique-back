using Einvoicing.Application.DTOs;

namespace Einvoicing.Application.Services;

public static class ExpenseActivityLogger
{
    public static TransactionActivityDto Create(
        string action,
        string description,
        Guid? authorId = null,
        string? authorName = null,
        DateTime? createdAt = null)
        => new(
            Guid.NewGuid(),
            authorId,
            authorName,
            action,
            description,
            createdAt ?? DateTime.UtcNow
        );
}
