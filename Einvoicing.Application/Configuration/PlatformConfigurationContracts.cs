namespace Einvoicing.Application.Configuration;

public static class PlatformConfigurationKeys
{
    public const string KycAutoApprovalThreshold = "Kyc.SeuilApprobationAutomatique";
    public const string KycManualReviewThreshold = "Kyc.SeuilRevisionManuelle";
}

public sealed record PlatformConfigurationDto(
    Guid Id,
    string Key,
    string Value,
    string ValueType,
    string Description,
    bool Locked,
    Guid? ModifiedBy,
    DateTime ModifiedAt);

public sealed record PlatformConfigurationHistoryDto(
    Guid Id,
    string Key,
    string OldValue,
    string NewValue,
    string Action,
    string Reason,
    Guid ModifiedBy,
    string ModifiedByName,
    DateTime ModifiedAt);

public interface IPlatformConfigurationService
{
    Task<IReadOnlyList<PlatformConfigurationDto>> ListAsync(CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct = default);
    Task<PlatformConfigurationDto> UpdateAsync(string key, string value, string reason, Guid actorId, CancellationToken ct = default);
    Task<PlatformConfigurationDto> SetLockAsync(string key, bool locked, string reason, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformConfigurationHistoryDto>> GetHistoryAsync(string key, CancellationToken ct = default);
}
