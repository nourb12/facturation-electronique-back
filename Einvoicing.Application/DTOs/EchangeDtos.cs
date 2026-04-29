namespace Einvoicing.Application.DTOs;

public record EnvoyerTtnRequest(Guid FactureId);

public record EchangeDto(
    Guid Id,
    Guid FactureId,
    string CorrelationId,
    string Statut,
    string? ReponseCode,
    string? ReponseMessage,
    string? MotifRejet,
    int RetryCount,
    DateTime CreeLe,
    DateTime? AccepteeA,
    DateTime? RejeteeLe
);
