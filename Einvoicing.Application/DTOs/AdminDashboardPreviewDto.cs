using System.ComponentModel.DataAnnotations;

namespace Einvoicing.Application.DTOs;

public sealed class PreviewCompanyRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
    [Required, RegularExpression(@"^[0-9]{7}/[A-Z]$")]
    public string TaxId { get; init; } = string.Empty;
}

public sealed record AdminDashboardPreviewDto(
    bool IsDemo, string Period, DateTimeOffset GeneratedAt,
    PreviewMetricDto[] Metrics, PreviewTaskDto[] Tasks, PreviewFlowDto[] Flow,
    PreviewTransmissionDto[] Transmission, PreviewServiceDto[] Services,
    PreviewCompanyDto[] Companies, PreviewPlanDto[] Plans,
    PreviewSecurityDto Security, PreviewActivityDto[] Activities,
    PreviewReasonDto[] RejectionReasons, PreviewCertificateDto[] Certificates,
    PreviewInvoiceDto[] Queue, PreviewRequestDto[] Requests, PreviewAnomalyDto[] Anomalies,
    decimal MonthlyRevenue, int RenewalsSoon, int InactiveCompanies, decimal AverageTransmissionSeconds);
public sealed record PreviewMetricDto(string Key, string Label, decimal Value, string Unit, string Detail, decimal Change, string ChangeUnit, bool PositiveIsGood);
public sealed record PreviewTaskDto(string Key, string Label, int Count, string Detail, string Tone);
public sealed record PreviewFlowDto(string Label, int Issued, int Accepted);
public sealed record PreviewTransmissionDto(string Key, string Label, int Count, string Color);
public sealed record PreviewServiceDto(string Name, string Status, string Detail, string Tone);
public sealed record PreviewCompanyDto(string Id, string Name, string TaxId, int Compliance, int? InactiveDays, string Plan);
public sealed record PreviewPlanDto(string Name, int Count, string Color);
public sealed record PreviewSecurityDto(int FailedLogins, int LockedAccounts, int AdminMfaPercent, int ActiveSessions);
public sealed record PreviewActivityDto(string Id, DateTimeOffset Date, string Actor, string Action, string Resource, string Ip, string Kind, string Tone, string Result);
public sealed record PreviewReasonDto(string Label, int Count);
public sealed record PreviewCertificateDto(string Company, DateTimeOffset ExpiresAt, int DaysLeft);
public sealed record PreviewInvoiceDto(string Number, string Company, decimal Amount, string Status, string Reason);
public sealed record PreviewRequestDto(string Id, string Company, string Contact, string Type, string Status, DateTimeOffset Date);
public sealed record PreviewAnomalyDto(string Reference, string Company, string Description, string Severity);
