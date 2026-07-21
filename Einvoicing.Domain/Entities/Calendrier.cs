namespace Einvoicing.Domain.Entities;

public sealed class CalendrierEvent
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public string Type { get; private set; } = "task";
    public string? StartTime { get; private set; }
    public string? EndTime { get; private set; }
    public string? LinkedClientName { get; private set; }
    public decimal? LinkedAmount { get; private set; }
    public string? ZoomLink { get; private set; }
    public int? ReminderMinutes { get; private set; }
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private CalendrierEvent() { }

    public static CalendrierEvent Creer(
        Guid entrepriseId,
        string title,
        DateOnly date,
        string type,
        string? startTime,
        string? endTime,
        string? linkedClientName,
        decimal? linkedAmount,
        string? zoomLink,
        int? reminderMinutes)
    {
        var evt = new CalendrierEvent
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            CreeLe = DateTime.UtcNow
        };
        evt.MettreAJour(title, date, type, startTime, endTime, linkedClientName, linkedAmount, zoomLink, reminderMinutes);
        return evt;
    }

    public void MettreAJour(
        string title,
        DateOnly date,
        string type,
        string? startTime,
        string? endTime,
        string? linkedClientName,
        decimal? linkedAmount,
        string? zoomLink,
        int? reminderMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title.Trim();
        Date = date;
        Type = string.IsNullOrWhiteSpace(type) ? "task" : type.Trim();
        StartTime = Nettoyer(startTime);
        EndTime = Nettoyer(endTime);
        LinkedClientName = Nettoyer(linkedClientName);
        LinkedAmount = linkedAmount;
        ZoomLink = Nettoyer(zoomLink);
        ReminderMinutes = reminderMinutes;
        ModifieLe = DateTime.UtcNow;
    }

    private static string? Nettoyer(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CalendrierTask
{
    public Guid Id { get; private set; }
    public Guid EntrepriseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public bool Done { get; private set; }
    public string Priority { get; private set; } = "medium";
    public DateTime CreeLe { get; private set; }
    public DateTime ModifieLe { get; private set; }

    private CalendrierTask() { }

    public static CalendrierTask Creer(Guid entrepriseId, string title, DateOnly date, string? priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new CalendrierTask
        {
            Id = Guid.NewGuid(),
            EntrepriseId = entrepriseId,
            Title = title.Trim(),
            Date = date,
            Priority = string.IsNullOrWhiteSpace(priority) ? "medium" : priority.Trim(),
            CreeLe = DateTime.UtcNow,
            ModifieLe = DateTime.UtcNow
        };
    }

    public void Basculer(bool? done = null)
    {
        Done = done ?? !Done;
        ModifieLe = DateTime.UtcNow;
    }
}
