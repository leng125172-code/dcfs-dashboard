using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Domain.Entities;
using WhaleDeck.Application.Services;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class GovernanceRepository(PlatformDbContext db) : IGovernanceRepository
{
    public async Task<IReadOnlyCollection<ScheduledTask>> ListSchedulesAsync(CancellationToken cancellationToken) =>
        await db.ScheduledTasks.AsNoTracking().OrderBy(item => item.Name).ToArrayAsync(cancellationToken);

    public async Task<ScheduledTask> SaveScheduleAsync(string actorSubject, SaveScheduledTaskCommand command, CancellationToken cancellationToken)
    {
        ScheduledTask item;
        if (command.Id is { } id)
        {
            item = await db.ScheduledTasks.SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Scheduled task was not found.");
            RequireVersion(command.ExpectedVersion, item.Version);
            item.TaskType = command.TaskType;
            item.Name = command.Name.Trim();
            item.ScheduleKind = command.ScheduleKind;
            item.ScheduleExpression = command.ScheduleExpression.Trim();
            item.Timezone = command.Timezone;
            item.ParametersJson = command.ParametersJson;
            item.ConcurrencyPolicy = command.ConcurrencyPolicy;
            item.TimeoutSeconds = command.TimeoutSeconds;
            item.IsEnabled = command.IsEnabled;
            item.UpdatedBySubject = actorSubject;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            item.NextRunAtUtc = item.IsEnabled ? ScheduleCalculator.NextUtc(item.ScheduleKind, item.ScheduleExpression, item.Timezone, DateTimeOffset.UtcNow) : null;
            item.Version++;
        }
        else
        {
            item = new ScheduledTask
            {
                TaskType = command.TaskType, Name = command.Name.Trim(), ScheduleKind = command.ScheduleKind,
                ScheduleExpression = command.ScheduleExpression.Trim(), Timezone = command.Timezone,
                ParametersJson = command.ParametersJson, ConcurrencyPolicy = command.ConcurrencyPolicy,
                TimeoutSeconds = command.TimeoutSeconds, IsEnabled = command.IsEnabled,
                NextRunAtUtc = command.IsEnabled ? ScheduleCalculator.NextUtc(command.ScheduleKind, command.ScheduleExpression, command.Timezone, DateTimeOffset.UtcNow) : null,
                CreatedBySubject = actorSubject, UpdatedBySubject = actorSubject
            };
            db.ScheduledTasks.Add(item);
        }
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task DeleteScheduleAsync(Guid id, long expectedVersion, CancellationToken cancellationToken)
    {
        var item = await db.ScheduledTasks.SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Scheduled task was not found.");
        RequireVersion(expectedVersion, item.Version);
        db.ScheduledTasks.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BackupPolicy>> ListBackupPoliciesAsync(CancellationToken cancellationToken) =>
        await db.BackupPolicies.AsNoTracking().OrderBy(item => item.InstanceResourceId).ToArrayAsync(cancellationToken);

    public async Task<BackupPolicy> SaveBackupPolicyAsync(SaveBackupPolicyCommand command, CancellationToken cancellationToken)
    {
        BackupPolicy item;
        if (command.Id is { } id)
        {
            item = await db.BackupPolicies.SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Backup policy was not found.");
            RequireVersion(command.ExpectedVersion, item.Version);
            item.IsEnabled = command.IsEnabled;
            item.ScheduleExpression = command.ScheduleExpression;
            item.Timezone = command.Timezone;
            item.RetentionCount = command.RetentionCount;
            item.RetentionDays = command.RetentionDays;
            item.TargetDirectoryId = command.TargetDirectoryId;
            item.Compression = command.Compression;
            item.VerifyAfterBackup = command.VerifyAfterBackup;
            item.CapacityWarningPercent = command.CapacityWarningPercent;
            item.CapacityCriticalPercent = command.CapacityCriticalPercent;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            item.Version++;
        }
        else
        {
            item = new BackupPolicy
            {
                InstanceResourceId = command.InstanceResourceId, IsEnabled = command.IsEnabled,
                ScheduleExpression = command.ScheduleExpression, Timezone = command.Timezone,
                RetentionCount = command.RetentionCount, RetentionDays = command.RetentionDays,
                TargetDirectoryId = command.TargetDirectoryId, Compression = command.Compression,
                VerifyAfterBackup = command.VerifyAfterBackup, CapacityWarningPercent = command.CapacityWarningPercent,
                CapacityCriticalPercent = command.CapacityCriticalPercent
            };
            db.BackupPolicies.Add(item);
        }
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<IReadOnlyCollection<AlertRule>> ListAlertRulesAsync(CancellationToken cancellationToken) =>
        await db.AlertRules.AsNoTracking().OrderBy(item => item.RuleType).ToArrayAsync(cancellationToken);

    public async Task<AlertRule> SaveAlertRuleAsync(SaveAlertRuleCommand command, CancellationToken cancellationToken)
    {
        AlertRule item;
        if (command.Id is { } id)
        {
            item = await db.AlertRules.SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("Alert rule was not found.");
            RequireVersion(command.ExpectedVersion, item.Version);
            item.RuleType = command.RuleType;
            item.ResourceSelectorJson = command.ResourceSelectorJson;
            item.ThresholdJson = command.ThresholdJson;
            item.EvaluationWindowSeconds = command.EvaluationWindowSeconds;
            item.Severity = command.Severity;
            item.IsEnabled = command.IsEnabled;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            item.Version++;
        }
        else
        {
            item = new AlertRule
            {
                RuleType = command.RuleType, ResourceSelectorJson = command.ResourceSelectorJson,
                ThresholdJson = command.ThresholdJson, EvaluationWindowSeconds = command.EvaluationWindowSeconds,
                Severity = command.Severity, IsEnabled = command.IsEnabled
            };
            db.AlertRules.Add(item);
        }
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<IReadOnlyCollection<AlertEvent>> ListAlertsAsync(bool includeRecovered, CancellationToken cancellationToken) =>
        await db.AlertEvents.AsNoTracking().Where(item => includeRecovered || item.State != "Recovered")
            .OrderByDescending(item => item.LastOccurredAtUtc).Take(500).ToArrayAsync(cancellationToken);

    public async Task<AlertEvent> UpdateAlertAsync(Guid id, string actorSubject, DateTimeOffset? silencedUntilUtc, CancellationToken cancellationToken)
    {
        var item = await db.AlertEvents.SingleOrDefaultAsync(value => value.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Alert event was not found.");
        item.AcknowledgedBySubject = actorSubject;
        item.AcknowledgedAtUtc = DateTimeOffset.UtcNow;
        item.SilencedUntilUtc = silencedUntilUtc;
        item.State = silencedUntilUtc is null ? "Acknowledged" : "Silenced";
        db.AlertEventHistory.Add(new AlertEventHistory { AlertEventId = item.Id, State = item.State, ActorSubject = actorSubject });
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<IReadOnlyCollection<PlatformSetting>> ListSettingsAsync(CancellationToken cancellationToken) =>
        await db.PlatformSettings.AsNoTracking().OrderBy(item => item.Key).ToArrayAsync(cancellationToken);

    public async Task<PlatformSetting> SaveSettingAsync(string key, string actorSubject, SavePlatformSettingCommand command, CancellationToken cancellationToken)
    {
        var item = await db.PlatformSettings.SingleOrDefaultAsync(value => value.Key == key, cancellationToken);
        if (item is null)
        {
            if (command.ExpectedVersion is not null) throw new InvalidOperationException("Setting version does not match.");
            item = new PlatformSetting { Key = key, ValueJson = command.ValueJson, UpdatedBySubject = actorSubject };
            db.PlatformSettings.Add(item);
        }
        else
        {
            RequireVersion(command.ExpectedVersion, item.Version);
            item.ValueJson = command.ValueJson;
            item.UpdatedBySubject = actorSubject;
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            item.Version++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<IReadOnlyCollection<AuditEvent>> ListAuditAsync(int take, CancellationToken cancellationToken) =>
        await db.AuditEvents.AsNoTracking().OrderByDescending(item => item.OccurredAtUtc).Take(take).ToArrayAsync(cancellationToken);

    private static void RequireVersion(long? expected, long actual)
    {
        if (expected != actual) throw new InvalidOperationException("Resource was modified by another request.");
    }
}
