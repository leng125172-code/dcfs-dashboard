using WhaleDeck.Application.Models;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Application.Abstractions;

public interface IPortalRepository
{
    Task<IReadOnlyCollection<PortalItem>> ListVisibleAsync(string subject, bool administrator, CancellationToken cancellationToken);
    Task<PortalItem> SaveAsync(string subject, bool administrator, SavePortalItemCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(string subject, bool administrator, Guid id, CancellationToken cancellationToken);
}

public interface IJobRepository
{
    Task<OperationJob> EnqueueAsync(OperationJob job, OutboxMessage outbox, AuditEvent audit, CancellationToken cancellationToken);
    Task<OperationJob?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OperationJob>> ListAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OperationJobEvent>> ListEventsAsync(Guid id, long afterSequence, CancellationToken cancellationToken);
    Task RequestCancellationAsync(Guid id, string actorSubject, CancellationToken cancellationToken);
}

public interface IAgentGateway
{
    Task<AgentHealthDto> GetHealthAsync(CancellationToken cancellationToken);
    Task<HostInfoDto> GetHostInfoAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ContainerDto>> ListContainersAsync(bool includeStopped, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ManagedResourceDto>> ListResourcesAsync(string kind, CancellationToken cancellationToken);
    Task<ManagedResourceDto> GetConfigRepositoryStatusAsync(CancellationToken cancellationToken);
    Task<ApplicationImageMetadataDto> InspectApplicationImageAsync(string image, bool pullIfMissing, CancellationToken cancellationToken);
    Task<PlanDto> PlanAsync(string area, string action, string? resourceId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<MetricValueDto>> GetMetricsSnapshotAsync(CancellationToken cancellationToken);
    Task<AgentOperationDto> ExecuteAsync(string area, string action, string resourceId, IReadOnlyDictionary<string, string> parameters, string? planHash, Guid jobId, string idempotencyKey, CancellationToken cancellationToken);
    Task<AgentOperationDto> GetOperationAsync(string operationId, CancellationToken cancellationToken);
    Task<AgentOperationDto> CancelOperationAsync(string operationId, CancellationToken cancellationToken);
}

public interface ICatalogProvider
{
    Task<CatalogResultDto> GetPopularAsync(CancellationToken cancellationToken);
}

public interface IOneTimeSecretStore
{
    Task<OneTimeSecretTicketDto> StoreAsync(string ownerSubject, string secret, CancellationToken cancellationToken);
    Task<string?> ConsumeAsync(string ownerSubject, string token, CancellationToken cancellationToken);
}

public interface IIdentityDirectory
{
    Task<CurrentUserDto> ResolveCurrentAsync(string subject, string? name, IReadOnlyCollection<string> claimGroups, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ManagedResourceDto>> ListUsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ManagedResourceDto>> ListGroupsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ManagedResourceDto>> ListSsoApplicationsAsync(CancellationToken cancellationToken);
}

public interface IIdentityManager
{
    Task ExecuteAsync(string action, string? resourceId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken);
}

public interface IManagementQuery
{
    Task<IReadOnlyCollection<ManagedResourceDto>> ListAsync(string area, CancellationToken cancellationToken);
}

public interface IMetricsQuery
{
    Task<IReadOnlyCollection<MetricSeriesSnapshotDto>> GetHistoryAsync(IReadOnlyCollection<string> kinds, int take, CancellationToken cancellationToken);
}

public interface IGovernanceRepository
{
    Task<IReadOnlyCollection<ScheduledTask>> ListSchedulesAsync(CancellationToken cancellationToken);
    Task<ScheduledTask> SaveScheduleAsync(string actorSubject, SaveScheduledTaskCommand command, CancellationToken cancellationToken);
    Task DeleteScheduleAsync(Guid id, long expectedVersion, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BackupPolicy>> ListBackupPoliciesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BackupRecord>> ListBackupRecordsAsync(string? instanceResourceId, int take, CancellationToken cancellationToken);
    Task<string> ResolveResourceExternalIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BackupPolicy> SaveBackupPolicyAsync(SaveBackupPolicyCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AlertRule>> ListAlertRulesAsync(CancellationToken cancellationToken);
    Task<AlertRule> SaveAlertRuleAsync(SaveAlertRuleCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AlertEvent>> ListAlertsAsync(bool includeRecovered, CancellationToken cancellationToken);
    Task<AlertEvent> UpdateAlertAsync(Guid id, string actorSubject, DateTimeOffset? silencedUntilUtc, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PlatformSetting>> ListSettingsAsync(CancellationToken cancellationToken);
    Task<PlatformSetting> SaveSettingAsync(string key, string actorSubject, SavePlatformSettingCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AuditEvent>> ListAuditAsync(int take, CancellationToken cancellationToken);
}
