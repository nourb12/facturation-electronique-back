using Einvoicing.Domain.Enums;

namespace Einvoicing.Domain.Entities;

/// <summary>
/// Entité représentant une demande de démonstration
/// </summary>
public sealed class DemoRequest
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Company { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Message { get; private set; }
    public DateTime PreferredDate { get; private set; }
    public string PreferredTime { get; private set; } = string.Empty;
    public DemoRequestStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private DemoRequest() { }

    public static DemoRequest Create(
        string firstName,
        string lastName,
        string email,
        string company,
        DateTime preferredDate,
        string preferredTime,
        string? phone = null,
        string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(company);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredTime);

        return new DemoRequest
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email.ToLowerInvariant().Trim(),
            Company = company.Trim(),
            Phone = phone?.Trim(),
            Message = message?.Trim(),
            PreferredDate = preferredDate.Date,
            PreferredTime = preferredTime.Trim(),
            Status = DemoRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateStatus(DemoRequestStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string firstName,
        string lastName,
        string email,
        string company,
        DateTime preferredDate,
        string preferredTime,
        string? phone = null,
        string? message = null)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.ToLowerInvariant().Trim();
        Company = company.Trim();
        Phone = phone?.Trim();
        Message = message?.Trim();
        PreferredDate = preferredDate.Date;
        PreferredTime = preferredTime.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
