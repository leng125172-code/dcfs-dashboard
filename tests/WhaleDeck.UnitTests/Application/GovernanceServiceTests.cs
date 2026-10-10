using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.UnitTests.Application;

public sealed class GovernanceServiceTests
{
    private readonly GovernanceService _service = new(new NoOpRepository());

    [Fact]
    public async Task ScheduleRejectsArbitraryTaskType()
    {
        var command = new SaveScheduledTaskCommand(null, "Shell", "bad", "Cron", "0 20 * * 0", "Asia/Shanghai", "{}", "Forbid", 300, true, null);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveScheduleAsync("admin", command, default));
    }

    [Fact]
    public async Task BackupRejectsUnregisteredTarget()
    {
        var command = new SaveBackupPolicyCommand(null, "database-platform.postgres", true, "0 2 * * *", "Asia/Shanghai", 14, 14,
            "/tmp", "Default", true, 80, 90, null);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveBackupPolicyAsync(command, default));
    }

    [Theory]
    [InlineData("secret.value")]
    [InlineData("unknown")]
    public async Task SettingsUseAllowList(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveSettingAsync(key, "admin", new("{}", null), default));
    }

    [Fact]
    public async Task AlertRejectsInvalidJson()
    {
        var command = new SaveAlertRuleCommand(null, "CpuUtilization", "{}", "not-json", 60, "Warning", true, null);
        await Assert.ThrowsAnyAsync<Exception>(() => _service.SaveAlertRuleAsync(command, default));
    }

    private sealed class NoOpRepository : IGovernanceRepository
    {
        public Task<IReadOnlyCollection<ScheduledTask>> ListSchedulesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<ScheduledTask>>([]);
        public Task<ScheduledTask> SaveScheduleAsync(string actorSubject, SaveScheduledTaskCommand command, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task DeleteScheduleAsync(Guid id, long expectedVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<BackupPolicy>> ListBackupPoliciesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<BackupPolicy>>([]);
        public Task<IReadOnlyCollection<BackupRecord>> ListBackupRecordsAsync(string? instanceResourceId, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<BackupRecord>>([]);
        public Task<string> ResolveResourceExternalIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult("database-platform.postgres");
        public Task<BackupPolicy> SaveBackupPolicyAsync(SaveBackupPolicyCommand command, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyCollection<AlertRule>> ListAlertRulesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AlertRule>>([]);
        public Task<AlertRule> SaveAlertRuleAsync(SaveAlertRuleCommand command, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyCollection<AlertEvent>> ListAlertsAsync(bool includeRecovered, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AlertEvent>>([]);
        public Task<AlertEvent> UpdateAlertAsync(Guid id, string actorSubject, DateTimeOffset? silencedUntilUtc, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyCollection<PlatformSetting>> ListSettingsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<PlatformSetting>>([]);
        public Task<PlatformSetting> SaveSettingAsync(string key, string actorSubject, SavePlatformSettingCommand command, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyCollection<AuditEvent>> ListAuditAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AuditEvent>>([]);
    }
}
