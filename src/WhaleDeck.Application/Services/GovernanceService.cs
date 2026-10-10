using System.Text.Json;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public sealed class GovernanceService(IGovernanceRepository repository)
{
    private static readonly HashSet<string> ScheduleTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Backup", "ApplicationUpdate", "SystemUpdate", "MetricsRollup", "RetentionCleanup" };
    private static readonly HashSet<string> SettingKeys = new(StringComparer.OrdinalIgnoreCase)
        { "maintenance.window", "applications.autoupdate", "metrics.retention", "catalog.refresh", "ui.branding" };
    private static readonly HashSet<string> AlertTypes = new(StringComparer.OrdinalIgnoreCase)
        { "CpuUtilization", "MemoryUtilization", "DiskUtilization", "ContainerHealth", "BackupFailure", "AgentOffline" };

    public async Task<IReadOnlyCollection<ScheduledTaskDto>> ListSchedulesAsync(CancellationToken cancellationToken) =>
        (await repository.ListSchedulesAsync(cancellationToken)).Select(Map).ToArray();

    public async Task<ScheduledTaskDto> SaveScheduleAsync(string actorSubject, SaveScheduledTaskCommand command, CancellationToken cancellationToken)
    {
        if (!ScheduleTypes.Contains(command.TaskType)) throw new ArgumentException("Unsupported scheduled task type.");
        ValidateName(command.Name);
        ValidateSchedule(command.ScheduleKind, command.ScheduleExpression);
        _ = ScheduleCalculator.NextUtc(command.ScheduleKind, command.ScheduleExpression, command.Timezone, DateTimeOffset.UtcNow);
        ValidateJson(command.ParametersJson);
        if (command.TimeoutSeconds is < 30 or > 86400) throw new ArgumentException("Schedule timeout is out of range.");
        if (command.ConcurrencyPolicy is not ("Forbid" or "Replace" or "Allow")) throw new ArgumentException("Invalid concurrency policy.");
        return Map(await repository.SaveScheduleAsync(actorSubject, command, cancellationToken));
    }

    public Task DeleteScheduleAsync(Guid id, long expectedVersion, CancellationToken cancellationToken) =>
        repository.DeleteScheduleAsync(id, expectedVersion, cancellationToken);

    public async Task<IReadOnlyCollection<BackupPolicyDto>> ListBackupPoliciesAsync(CancellationToken cancellationToken)
    {
        var policies = await repository.ListBackupPoliciesAsync(cancellationToken);
        return await Task.WhenAll(policies.Select(async item => new BackupPolicyDto(item.Id,
            await repository.ResolveResourceExternalIdAsync(item.InstanceResourceId, cancellationToken), item.IsEnabled,
            item.ScheduleExpression, item.Timezone, item.RetentionCount, item.RetentionDays, item.TargetDirectoryId,
            item.Compression, item.VerifyAfterBackup, item.CapacityWarningPercent, item.CapacityCriticalPercent, item.Version)));
    }

    public async Task<BackupPolicyDto> SaveBackupPolicyAsync(SaveBackupPolicyCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.InstanceResourceId) || command.InstanceResourceId.Length > 160)
            throw new ArgumentException("Managed database resource id is invalid.");
        ValidateSchedule("Cron", command.ScheduleExpression);
        if (command.RetentionCount is < 1 or > 365 || command.RetentionDays is < 1 or > 3650) throw new ArgumentException("Backup retention is out of range.");
        if (!string.Equals(command.TargetDirectoryId, "database-platform.backup.hdd", StringComparison.Ordinal)) throw new ArgumentException("Backup target is not approved.");
        if (command.CapacityWarningPercent is < 50 or > 95 || command.CapacityCriticalPercent <= command.CapacityWarningPercent || command.CapacityCriticalPercent > 99)
            throw new ArgumentException("Backup capacity thresholds are invalid.");
        var item = await repository.SaveBackupPolicyAsync(command, cancellationToken);
        return new BackupPolicyDto(item.Id, await repository.ResolveResourceExternalIdAsync(item.InstanceResourceId, cancellationToken), item.IsEnabled, item.ScheduleExpression, item.Timezone,
            item.RetentionCount, item.RetentionDays, item.TargetDirectoryId, item.Compression, item.VerifyAfterBackup,
            item.CapacityWarningPercent, item.CapacityCriticalPercent, item.Version);
    }

    public async Task<IReadOnlyCollection<AlertRuleDto>> ListAlertRulesAsync(CancellationToken cancellationToken) =>
        (await repository.ListAlertRulesAsync(cancellationToken)).Select(Map).ToArray();

    public async Task<AlertRuleDto> SaveAlertRuleAsync(SaveAlertRuleCommand command, CancellationToken cancellationToken)
    {
        if (!AlertTypes.Contains(command.RuleType)) throw new ArgumentException("Unsupported alert rule type.");
        ValidateJson(command.ResourceSelectorJson);
        ValidateJson(command.ThresholdJson);
        if (command.EvaluationWindowSeconds is < 12 or > 86400) throw new ArgumentException("Alert evaluation window is out of range.");
        if (command.Severity is not ("Info" or "Warning" or "Critical")) throw new ArgumentException("Invalid alert severity.");
        return Map(await repository.SaveAlertRuleAsync(command, cancellationToken));
    }

    public async Task<IReadOnlyCollection<AlertEventDto>> ListAlertsAsync(bool includeRecovered, CancellationToken cancellationToken) =>
        (await repository.ListAlertsAsync(includeRecovered, cancellationToken)).Select(Map).ToArray();

    public async Task<AlertEventDto> UpdateAlertAsync(Guid id, string actorSubject, DateTimeOffset? silencedUntilUtc, CancellationToken cancellationToken)
    {
        if (silencedUntilUtc is { } until && (until <= DateTimeOffset.UtcNow || until > DateTimeOffset.UtcNow.AddDays(30)))
            throw new ArgumentException("Silence expiration is out of range.");
        return Map(await repository.UpdateAlertAsync(id, actorSubject, silencedUntilUtc, cancellationToken));
    }

    public async Task<IReadOnlyCollection<PlatformSettingDto>> ListSettingsAsync(CancellationToken cancellationToken) =>
        (await repository.ListSettingsAsync(cancellationToken)).Select(item => new PlatformSettingDto(item.Key, item.ValueJson, item.Version, item.UpdatedAtUtc)).ToArray();

    public async Task<PlatformSettingDto> SaveSettingAsync(string key, string actorSubject, SavePlatformSettingCommand command, CancellationToken cancellationToken)
    {
        if (!SettingKeys.Contains(key)) throw new ArgumentException("Setting key is not approved.");
        ValidateJson(command.ValueJson);
        var item = await repository.SaveSettingAsync(key, actorSubject, command, cancellationToken);
        return new PlatformSettingDto(item.Key, item.ValueJson, item.Version, item.UpdatedAtUtc);
    }

    public async Task<IReadOnlyCollection<AuditEventDto>> ListAuditAsync(int take, CancellationToken cancellationToken) =>
        (await repository.ListAuditAsync(Math.Clamp(take, 1, 500), cancellationToken)).Select(item => new AuditEventDto(item.Id,
            item.ActorSubject, item.Action, item.TargetType, item.TargetId, item.Result, item.SourceIp, item.JobId, item.TraceId,
            item.OccurredAtUtc)).ToArray();

    private static ScheduledTaskDto Map(Domain.Entities.ScheduledTask item) => new(item.Id, item.TaskType, item.Name,
        item.ScheduleKind, item.ScheduleExpression, item.Timezone, item.ConcurrencyPolicy, item.TimeoutSeconds, item.IsEnabled,
        item.NextRunAtUtc, item.Version);
    private static AlertRuleDto Map(Domain.Entities.AlertRule item) => new(item.Id, item.RuleType, item.ResourceSelectorJson,
        item.ThresholdJson, item.EvaluationWindowSeconds, item.Severity, item.IsEnabled, item.Version);
    private static AlertEventDto Map(Domain.Entities.AlertEvent item) => new(item.Id, item.RuleId, item.ResourceId, item.State,
        item.Severity, item.OccurrenceCount, item.FirstOccurredAtUtc, item.LastOccurredAtUtc, item.RecoveredAtUtc,
        item.AcknowledgedBySubject, item.AcknowledgedAtUtc, item.SilencedUntilUtc, item.SummaryCode);

    private static void ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120) throw new ArgumentException("Name is required and must be at most 120 characters.");
    }
    private static void ValidateSchedule(string kind, string expression)
    {
        if (kind is not ("Cron" or "Interval" or "Daily")) throw new ArgumentException("Unsupported schedule kind.");
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 128) throw new ArgumentException("Schedule expression is invalid.");
        if (kind == "Cron" && expression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length != 5) throw new ArgumentException("Cron expression must have five fields.");
    }
    private static void ValidateJson(string json)
    {
        if (json.Length > 32768) throw new ArgumentException("JSON value is too large.");
        using var _ = JsonDocument.Parse(json);
    }
}
