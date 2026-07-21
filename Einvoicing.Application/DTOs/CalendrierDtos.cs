namespace Einvoicing.Application.DTOs;

public record CalendrierEventDto(
    Guid Id,
    Guid EntrepriseId,
    string Title,
    DateOnly Date,
    string Type,
    string? StartTime,
    string? EndTime,
    string? LinkedClientName,
    decimal? LinkedAmount,
    string? ZoomLink,
    int? ReminderMinutes
);

public record CalendrierEventRequest(
    string Title,
    DateOnly Date,
    string Type,
    string? StartTime,
    string? EndTime,
    string? LinkedClientName,
    decimal? LinkedAmount,
    string? ZoomLink,
    int? ReminderMinutes
);

public record CalendrierTaskDto(
    Guid Id,
    Guid EntrepriseId,
    string Title,
    DateOnly Date,
    bool Done,
    string Priority
);

public record CalendrierTaskRequest(
    string Title,
    DateOnly Date,
    string? Priority
);

public record ToggleCalendrierTaskRequest(bool? Done);
