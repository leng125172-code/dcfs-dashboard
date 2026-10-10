using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;
using WhaleDeck.Domain.Entities;
using WhaleDeck.Infrastructure.Persistence;

namespace WhaleDeck.Worker;

public sealed class Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogCycleFailure = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1001, "WorkerCycleFailed"),
        "Worker cycle failed with diagnostic code WORKER_CYCLE_FAILED");

    private DateTimeOffset _nextCatalogRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _nextRetentionSweep = DateTimeOffset.MinValue;
    private readonly string _workerInstanceId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(6));
        do
        {
            try
            {
                await RunCycle(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogCycleFailure(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunCycle(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var agent = scope.ServiceProvider.GetRequiredService<IAgentGateway>();
        var secrets = scope.ServiceProvider.GetRequiredService<IOneTimeSecretStore>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityManager>();
        var leases = scope.ServiceProvider.GetRequiredService<ResourceLeaseManager>();
        await CollectMetrics(db, agent, cancellationToken);
        await SyncManagedResources(db, agent, cancellationToken);
        await DispatchScheduledTasks(db, agent, cancellationToken);
        await DispatchBackupPolicies(db, cancellationToken);
        await DispatchOutbox(db, agent, secrets, identity, leases, _workerInstanceId, cancellationToken);
        await ReconcileAgentOperations(db, agent, leases, _workerInstanceId, cancellationToken);
        await EvaluateAlerts(db, cancellationToken);

        if (_nextCatalogRefresh <= DateTimeOffset.UtcNow)
        {
            await scope.ServiceProvider.GetRequiredService<ICatalogProvider>().GetPopularAsync(cancellationToken);
            _nextCatalogRefresh = DateTimeOffset.UtcNow.AddHours(1);
        }
        if (_nextRetentionSweep <= DateTimeOffset.UtcNow)
        {
            await ApplyRollups(db, cancellationToken);
            await ApplyRetention(db, cancellationToken);
            _nextRetentionSweep = DateTimeOffset.UtcNow.AddHours(1);
        }
    }

    private static async Task CollectMetrics(PlatformDbContext db, IAgentGateway agent, CancellationToken cancellationToken)
    {
        var health = await agent.GetHealthAsync(cancellationToken);
        if (!health.Available) return;
        var resource = await db.ManagedResources.SingleOrDefaultAsync(item => item.ResourceType == "Host" && item.ExternalId == "local", cancellationToken);
        if (resource is null)
        {
            resource = new ManagedResource { ResourceType = "Host", ExternalId = "local", DisplayName = "Precision-7920-Tower", ProtectionLevel = "ControlPlane", Source = "Agent" };
            db.ManagedResources.Add(resource);
        }
        var snapshot = await agent.GetMetricsSnapshotAsync(cancellationToken);
        // A host can report the same logical metric through overlapping kernel
        // views in one sample. Collapse the batch before EF tracks new series,
        // otherwise two not-yet-saved entities can race the unique index.
        foreach (var metric in snapshot
            .GroupBy(item => (item.Kind, item.DeviceId))
            .Select(group => group.MaxBy(item => item.SampledAtUtc)!))
        {
            var dimensions = JsonSerializer.Serialize(new { metric.DeviceId });
            var series = await db.MetricSeries.SingleOrDefaultAsync(item => item.ResourceId == resource.Id && item.MetricKind == metric.Kind && item.DimensionsJson == dimensions, cancellationToken);
            if (series is null)
            {
                series = new MetricSeries { ResourceId = resource.Id, MetricKind = metric.Kind, Unit = metric.Unit, DimensionsJson = dimensions };
                db.MetricSeries.Add(series);
            }
            series.LastSeenAtUtc = metric.SampledAtUtc;
            db.MetricSamples.Add(new MetricSample { SeriesId = series.Id, SampledAtUtc = metric.SampledAtUtc, ValueDouble = metric.Value, Quality = metric.Quality });
        }
        resource.LastSeenAtUtc = health.ObservedAtUtc;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SyncManagedResources(PlatformDbContext db, IAgentGateway agent, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var discovered = (await agent.ListResourcesAsync("overview", cancellationToken))
            .Concat(await agent.ListResourcesAsync("databases", cancellationToken))
            .Concat(await agent.ListResourcesAsync("applications", cancellationToken))
            .ToArray();
        foreach (var item in discovered)
        {
            var resource = await db.ManagedResources.SingleOrDefaultAsync(value => value.ResourceType == item.Type && value.ExternalId == item.Id, cancellationToken);
            if (resource is null)
            {
                resource = new ManagedResource
                {
                    ResourceType = item.Type, ExternalId = item.Id, DisplayName = item.Name,
                    ProtectionLevel = item.IsProtected ? "Protected" : "Managed", Source = "Agent"
                };
                db.ManagedResources.Add(resource);
            }
            resource.DisplayName = item.Name;
            resource.ProtectionLevel = item.IsProtected ? "Protected" : "Managed";
            resource.LabelsJson = JsonSerializer.Serialize(new { state = item.State, version = item.Version, attributes = item.Attributes });
            resource.LastSeenAtUtc = now;
            resource.Version++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task DispatchScheduledTasks(PlatformDbContext db, IAgentGateway agent, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var schedules = await db.ScheduledTasks.Where(item => item.IsEnabled && item.NextRunAtUtc != null && item.NextRunAtUtc <= now)
            .OrderBy(item => item.NextRunAtUtc).Take(20).ToArrayAsync(cancellationToken);
        foreach (var schedule in schedules)
        {
            var scheduledFor = schedule.NextRunAtUtc!.Value;
            if (await db.ScheduledTaskRuns.AnyAsync(item => item.ScheduleId == schedule.Id && item.ScheduledForUtc == scheduledFor, cancellationToken))
            {
                schedule.NextRunAtUtc = ScheduleCalculator.NextUtc(schedule.ScheduleKind, schedule.ScheduleExpression, schedule.Timezone, scheduledFor);
                continue;
            }

            using var parametersDocument = JsonDocument.Parse(schedule.ParametersJson);
            var root = parametersDocument.RootElement;
            var (area, action, defaultResource) = schedule.TaskType switch
            {
                "Backup" => ("backups", "run", string.Empty),
                "ApplicationUpdate" => ("applications", "update", string.Empty),
                "SystemUpdate" => ("host", "update-install", "whaledeck.host"),
                "MetricsRollup" => ("internal", "metrics-rollup", string.Empty),
                "RetentionCleanup" => ("internal", "retention-cleanup", string.Empty),
                _ => throw new InvalidOperationException("A scheduled task type is unsupported.")
            };
            var resourceId = root.TryGetProperty("resourceId", out var resourceValue) ? resourceValue.GetString() ?? defaultResource : defaultResource;
            var parameters = root.TryGetProperty("parameters", out var parameterValue) && parameterValue.ValueKind == JsonValueKind.Object
                ? parameterValue.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.ToString(), StringComparer.Ordinal)
                : new Dictionary<string, string>();
            if (schedule.TaskType == "ApplicationUpdate") parameters["automatic"] = "true";
            string? planHash = null;
            if (action is "update" or "update-install")
                planHash = (await agent.PlanAsync(area, action, resourceId, parameters, cancellationToken)).PlanHash;

            var job = new OperationJob
            {
                JobType = $"{area}.{action}", ActorSubject = "system:scheduler",
                IdempotencyKey = $"schedule:{schedule.Id:D}:{scheduledFor.ToUnixTimeSeconds()}",
                RequestJson = JsonSerializer.Serialize(new { resourceId, parameters, planHash })
            };
            db.OperationJobs.Add(job);
            db.ScheduledTaskRuns.Add(new ScheduledTaskRun { ScheduleId = schedule.Id, JobId = job.Id, ScheduledForUtc = scheduledFor });
            db.AuditEvents.Add(new AuditEvent
            {
                ActorSubject = "system:scheduler", Action = job.JobType, TargetType = "schedule", TargetId = schedule.Id.ToString("D"),
                Result = "Accepted", JobId = job.Id, TraceId = job.Id.ToString("N")
            });
            if (area == "internal")
            {
                if (action == "metrics-rollup") await ApplyRollups(db, cancellationToken);
                if (action == "retention-cleanup") await ApplyRetention(db, cancellationToken);
                job.State = "Succeeded";
                job.Phase = "Completed";
                job.ProgressPercent = 100;
                job.StartedAtUtc = now;
                job.CompletedAtUtc = now;
            }
            else
            {
                db.OutboxMessages.Add(new OutboxMessage { MessageType = "OperationRequested", PayloadJson = JsonSerializer.Serialize(new { jobId = job.Id }) });
            }
            schedule.NextRunAtUtc = ScheduleCalculator.NextUtc(schedule.ScheduleKind, schedule.ScheduleExpression, schedule.Timezone, scheduledFor);
            schedule.Version++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task DispatchBackupPolicies(PlatformDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var policies = await db.BackupPolicies.Where(item => item.IsEnabled).Take(50).ToArrayAsync(cancellationToken);
        foreach (var policy in policies)
        {
            var latest = await db.BackupRecords.Where(item => item.PolicyId == policy.Id)
                .OrderByDescending(item => item.StartedAtUtc).Select(item => (DateTimeOffset?)item.StartedAtUtc).FirstOrDefaultAsync(cancellationToken);
            var after = latest ?? policy.UpdatedAtUtc.AddMinutes(-1);
            var next = ScheduleCalculator.NextUtc("Cron", policy.ScheduleExpression, policy.Timezone, after);
            if (next > now) continue;
            var resource = await db.ManagedResources.SingleOrDefaultAsync(item => item.Id == policy.InstanceResourceId, cancellationToken);
            if (resource is null) continue;
            var job = new OperationJob
            {
                JobType = "backups.run", ActorSubject = "system:backup-policy",
                IdempotencyKey = $"backup-policy:{policy.Id:D}:{next.ToUnixTimeSeconds()}",
                RequestJson = JsonSerializer.Serialize(new { resourceId = resource.ExternalId, parameters = new Dictionary<string, string>(), planHash = (string?)null })
            };
            db.OperationJobs.Add(job);
            db.BackupRecords.Add(new BackupRecord
            {
                InstanceResourceId = policy.InstanceResourceId, PolicyId = policy.Id, JobId = job.Id,
                ExpiresAtUtc = now.AddDays(policy.RetentionDays)
            });
            db.OutboxMessages.Add(new OutboxMessage { MessageType = "OperationRequested", PayloadJson = JsonSerializer.Serialize(new { jobId = job.Id }) });
            db.AuditEvents.Add(new AuditEvent
            {
                ActorSubject = "system:backup-policy", Action = "backups.run", TargetType = "backup-policy", TargetId = policy.Id.ToString("D"),
                Result = "Accepted", JobId = job.Id, TraceId = job.Id.ToString("N")
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task DispatchOutbox(
        PlatformDbContext db,
        IAgentGateway agent,
        IOneTimeSecretStore secrets,
        IIdentityManager identity,
        ResourceLeaseManager leases,
        string workerInstanceId,
        CancellationToken cancellationToken)
    {
        var messages = await db.OutboxMessages.Where(item => item.ProcessedAtUtc == null && item.AvailableAtUtc <= DateTimeOffset.UtcNow)
            .OrderBy(item => item.OccurredAtUtc).Take(10).ToArrayAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                if (message.MessageType == "OperationRequested")
                {
                    using var payload = JsonDocument.Parse(message.PayloadJson);
                    var jobId = payload.RootElement.GetProperty("jobId").GetGuid();
                    var job = await db.OperationJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
                    var lockKey = GetResourceLockKey(job);
                    if (!await leases.TryAcquireAsync(lockKey, job.Id, workerInstanceId, cancellationToken))
                    {
                        message.AvailableAtUtc = DateTimeOffset.UtcNow.AddSeconds(6);
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }
                    if (job.CancelRequestedAtUtc is not null)
                    {
                        await Complete(db, job, "Canceled", "Canceled", null, cancellationToken);
                    }
                    else
                    {
                        var split = job.JobType.Split('.', 2);
                        using var request = JsonDocument.Parse(job.RequestJson);
                        var resourceId = request.RootElement.TryGetProperty("resourceId", out var resource) ? resource.GetString() ?? string.Empty : string.Empty;
                        var parameters = request.RootElement.TryGetProperty("parameters", out var values) && values.ValueKind == JsonValueKind.Object
                            ? values.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.ToString(), StringComparer.Ordinal)
                            : new Dictionary<string, string>();
                        var planHash = request.RootElement.TryGetProperty("planHash", out var planHashElement) && planHashElement.ValueKind == JsonValueKind.String
                            ? planHashElement.GetString()
                            : null;
                        if (parameters.TryGetValue("inputTicket", out var inputTicket))
                        {
                            var secret = await secrets.ConsumeAsync(job.ActorSubject, inputTicket, cancellationToken)
                                ?? throw new InvalidOperationException("The staged secret expired before the operation was dispatched.");
                            var target = parameters.TryGetValue("inputKind", out var configuredTarget) ? configuredTarget : "password";
                            if (target is not ("password" or "environment")) throw new InvalidOperationException("The staged secret target is unsupported.");
                            parameters[target] = secret;
                            var successor = await secrets.StoreAsync(job.ActorSubject, secret, cancellationToken);
                            parameters["inputTicket"] = successor.Token;
                            var persistedParameters = parameters
                                .Where(item => item.Key != target)
                                .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
                            job.RequestJson = JsonSerializer.Serialize(new { resourceId, parameters = persistedParameters, planHash });
                            job.ResultJson = JsonSerializer.Serialize(new { secretToken = successor.Token, secretExpiresAtUtc = successor.ExpiresAtUtc });
                        }

                        job.State = "Running";
                        job.Phase = "DispatchingToAgent";
                        job.StartedAtUtc ??= DateTimeOffset.UtcNow;
                        job.Version++;
                        await db.SaveChangesAsync(cancellationToken);
                        if (split[0] == "identity")
                        {
                            await identity.ExecuteAsync(split[1], resourceId, parameters, cancellationToken);
                            await Complete(db, job, "Succeeded", "Completed", null, cancellationToken, 100);
                        }
                        else
                        {
                            var operation = await agent.ExecuteAsync(split[0], split[1], resourceId, parameters, planHash, job.Id, job.IdempotencyKey, cancellationToken);
                            job.AgentOperationId = Guid.TryParse(operation.OperationId, out var operationId) ? operationId : null;
                            await ApplyAgentState(db, job, operation, cancellationToken);
                        }
                    }
                    if (job.CompletedAtUtc is not null)
                    {
                        await leases.ReleaseAsync(lockKey, job.Id, workerInstanceId, cancellationToken);
                    }
                }
                else if (message.MessageType == "OperationCancellationRequested")
                {
                    using var payload = JsonDocument.Parse(message.PayloadJson);
                    var jobId = payload.RootElement.GetProperty("jobId").GetGuid();
                    var job = await db.OperationJobs.SingleAsync(item => item.Id == jobId, cancellationToken);
                    var lockKey = GetResourceLockKey(job);
                    if (!await leases.TryAcquireAsync(lockKey, job.Id, workerInstanceId, cancellationToken))
                    {
                        message.AvailableAtUtc = DateTimeOffset.UtcNow.AddSeconds(6);
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }
                    if (job.CompletedAtUtc is null)
                    {
                        if (job.AgentOperationId is { } operationId)
                        {
                            var operation = await agent.CancelOperationAsync(operationId.ToString("D"), cancellationToken);
                            await ApplyAgentState(db, job, operation, cancellationToken);
                        }
                        else
                        {
                            await Complete(db, job, "Canceled", "CanceledBeforeDispatch", null, cancellationToken);
                        }
                    }
                    if (job.CompletedAtUtc is not null)
                    {
                        await leases.ReleaseAsync(lockKey, job.Id, workerInstanceId, cancellationToken);
                    }
                }
                message.ProcessedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                message.Attempts++;
                var secretExpired = exception is InvalidOperationException && exception.Message.Contains("staged secret expired", StringComparison.Ordinal);
                var terminal = secretExpired || message.Attempts >= 8;
                message.LastErrorCode = secretExpired ? "ONE_TIME_SECRET_EXPIRED" : "OUTBOX_DISPATCH_FAILED";
                if (terminal)
                {
                    message.ProcessedAtUtc = DateTimeOffset.UtcNow;
                    if (message.MessageType is "OperationRequested" or "OperationCancellationRequested")
                    {
                        using var failedPayload = JsonDocument.Parse(message.PayloadJson);
                        var failedJobId = failedPayload.RootElement.GetProperty("jobId").GetGuid();
                        var failedJob = await db.OperationJobs.SingleAsync(item => item.Id == failedJobId, cancellationToken);
                        await Complete(db, failedJob, "Failed", secretExpired ? "SecretExpired" : "DispatchRetriesExhausted", message.LastErrorCode, cancellationToken);
                    }
                }
                else
                {
                    message.AvailableAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, message.Attempts)));
                }
                await db.SaveChangesAsync(cancellationToken);
                if (!terminal) throw;
            }
        }
    }

    private static async Task ReconcileAgentOperations(
        PlatformDbContext db,
        IAgentGateway agent,
        ResourceLeaseManager leases,
        string workerInstanceId,
        CancellationToken cancellationToken)
    {
        var jobs = await db.OperationJobs
            .Where(item => item.AgentOperationId != null && item.CompletedAtUtc == null)
            .OrderBy(item => item.StartedAtUtc)
            .Take(50)
            .ToArrayAsync(cancellationToken);

        foreach (var job in jobs)
        {
            var lockKey = GetResourceLockKey(job);
            if (!await leases.TryAcquireAsync(lockKey, job.Id, workerInstanceId, cancellationToken)) continue;
            var operationId = job.AgentOperationId!.Value.ToString("D");
            var operation = job.CancelRequestedAtUtc is not null
                ? await agent.CancelOperationAsync(operationId, cancellationToken)
                : await agent.GetOperationAsync(operationId, cancellationToken);
            await ApplyAgentState(db, job, operation, cancellationToken);
            if (job.CompletedAtUtc is not null)
            {
                await leases.ReleaseAsync(lockKey, job.Id, workerInstanceId, cancellationToken);
            }
        }
    }

    private static async Task EvaluateAlerts(PlatformDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rules = await db.AlertRules.Where(item => item.IsEnabled).ToArrayAsync(cancellationToken);
        foreach (var rule in rules)
        {
            var triggeredResources = new List<Guid?>();
            switch (rule.RuleType)
            {
                case "CpuUtilization":
                case "MemoryUtilization":
                case "DiskUtilization":
                {
                    var kind = rule.RuleType switch
                    {
                        "CpuUtilization" => "cpu.utilization",
                        "MemoryUtilization" => "memory.utilization",
                        _ => "filesystem.utilization"
                    };
                    var threshold = ReadThreshold(rule.ThresholdJson);
                    var cutoff = now.AddSeconds(-rule.EvaluationWindowSeconds);
                    var series = await db.MetricSeries.Where(item => item.MetricKind == kind).ToArrayAsync(cancellationToken);
                    foreach (var item in series)
                    {
                        var average = await db.MetricSamples.Where(sample => sample.SeriesId == item.Id && sample.SampledAtUtc >= cutoff && sample.ValueDouble != null)
                            .AverageAsync(sample => sample.ValueDouble, cancellationToken);
                        if (average >= threshold) triggeredResources.Add(item.ResourceId);
                    }
                    break;
                }
                case "AgentOffline":
                {
                    var host = await db.ManagedResources.SingleOrDefaultAsync(item => item.ResourceType == "Host" && item.ExternalId == "local", cancellationToken);
                    if (host is null || host.LastSeenAtUtc < now.AddSeconds(-rule.EvaluationWindowSeconds)) triggeredResources.Add(host?.Id);
                    break;
                }
                case "ContainerHealth":
                {
                    var containers = await db.ManagedResources.Where(item => item.ResourceType == "Container").ToArrayAsync(cancellationToken);
                    foreach (var container in containers)
                    {
                        using var labels = JsonDocument.Parse(container.LabelsJson);
                        var state = labels.RootElement.TryGetProperty("state", out var stateValue) ? stateValue.GetString() : null;
                        if (state is not "running") triggeredResources.Add(container.Id);
                    }
                    break;
                }
                case "BackupFailure":
                    triggeredResources.AddRange(await db.BackupRecords.Where(item => item.Status == "Failed" && item.CompletedAtUtc >= now.AddSeconds(-rule.EvaluationWindowSeconds))
                        .Select(item => (Guid?)item.InstanceResourceId).Distinct().ToArrayAsync(cancellationToken));
                    break;
            }

            var active = await db.AlertEvents.Where(item => item.RuleId == rule.Id && item.State != "Recovered").ToArrayAsync(cancellationToken);
            foreach (var resourceId in triggeredResources.Distinct())
            {
                var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{rule.Id:D}:{resourceId?.ToString("D") ?? "global"}")));
                var existing = active.SingleOrDefault(item => item.Fingerprint == fingerprint);
                if (existing is null)
                {
                    existing = new AlertEvent
                    {
                        RuleId = rule.Id, ResourceId = resourceId, Fingerprint = fingerprint, Severity = rule.Severity,
                        SummaryCode = $"ALERT_{rule.RuleType.ToUpperInvariant()}", DetailJson = rule.ThresholdJson
                    };
                    db.AlertEvents.Add(existing);
                    db.AlertEventHistory.Add(new AlertEventHistory { AlertEventId = existing.Id, State = "Active", ActorSubject = "system:alert-evaluator" });
                }
                else if (existing.LastOccurredAtUtc <= now.AddSeconds(-rule.EvaluationWindowSeconds))
                {
                    existing.LastOccurredAtUtc = now;
                    existing.OccurrenceCount++;
                }
            }

            var triggeredIds = triggeredResources.Distinct().ToHashSet();
            foreach (var existing in active.Where(item => !triggeredIds.Contains(item.ResourceId)))
            {
                existing.State = "Recovered";
                existing.RecoveredAtUtc = now;
                existing.LastOccurredAtUtc = now;
                db.AlertEventHistory.Add(new AlertEventHistory { AlertEventId = existing.Id, State = "Recovered", ActorSubject = "system:alert-evaluator" });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static double ReadThreshold(string thresholdJson)
    {
        using var document = JsonDocument.Parse(thresholdJson);
        return document.RootElement.TryGetProperty("value", out var value) && value.TryGetDouble(out var threshold)
            ? threshold : throw new InvalidOperationException("Alert threshold JSON requires a numeric value.");
    }

    private static string GetResourceLockKey(OperationJob job)
    {
        using var request = JsonDocument.Parse(job.RequestJson);
        var resourceId = request.RootElement.TryGetProperty("resourceId", out var resource) && resource.ValueKind == JsonValueKind.String
            ? resource.GetString()
            : null;
        var area = job.JobType.Split('.', 2)[0];
        var identity = $"{area}:{resourceId ?? "global"}";
        return $"{area}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))}";
    }

    private static Task ApplyAgentState(
        PlatformDbContext db,
        OperationJob job,
        AgentOperationDto operation,
        CancellationToken cancellationToken)
    {
        var transition = AgentOperationStateMapper.Map(operation);
        return Complete(db, job, transition.State, transition.Phase, transition.ErrorCode, cancellationToken, transition.ProgressPercent);
    }

    private static async Task Complete(
        PlatformDbContext db,
        OperationJob job,
        string state,
        string phase,
        string? errorCode,
        CancellationToken cancellationToken,
        short? progressPercent = null)
    {
        if (job.State == state && job.Phase == phase && job.ProgressPercent == progressPercent && job.ErrorCode == errorCode)
        {
            return;
        }
        job.State = state;
        job.Phase = phase;
        job.ErrorCode = errorCode;
        job.ProgressPercent = state == "Succeeded" ? (short)100 : progressPercent;
        if (state is "Succeeded" or "Failed" or "Canceled") job.CompletedAtUtc = DateTimeOffset.UtcNow;
        job.Version++;
        var sequence = await db.OperationJobEvents.Where(item => item.JobId == job.Id).MaxAsync(item => (long?)item.Sequence, cancellationToken) ?? 0;
        db.OperationJobEvents.Add(new OperationJobEvent { JobId = job.Id, Sequence = sequence + 1, State = state, Phase = phase, ProgressPercent = job.ProgressPercent, MessageCode = $"JOB_{state.ToUpperInvariant()}" });
        var backup = await db.BackupRecords.SingleOrDefaultAsync(item => item.JobId == job.Id, cancellationToken);
        if (backup is not null && state is "Succeeded" or "Failed" or "Canceled")
        {
            backup.Status = state;
            backup.CompletedAtUtc = DateTimeOffset.UtcNow;
            backup.ErrorCode = errorCode;
            if (state == "Succeeded") backup.VerifiedAtUtc = DateTimeOffset.UtcNow;
        }
        var scheduleRun = await db.ScheduledTaskRuns.SingleOrDefaultAsync(item => item.JobId == job.Id, cancellationToken);
        if (scheduleRun is not null && state is "Succeeded" or "Failed" or "Canceled")
        {
            scheduleRun.Result = state;
            scheduleRun.CompletedAtUtc = DateTimeOffset.UtcNow;
            scheduleRun.StartedAtUtc ??= job.StartedAtUtc;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task ApplyRetention(PlatformDbContext db, CancellationToken cancellationToken)
    {
        var metricCutoff = DateTimeOffset.UtcNow.AddDays(-14);
        var eventCutoff = DateTimeOffset.UtcNow.AddDays(-90);
        await db.MetricSamples.Where(item => item.SampledAtUtc < metricCutoff).ExecuteDeleteAsync(cancellationToken);
        await db.MetricRollups.Where(item => item.WindowStartUtc < metricCutoff).ExecuteDeleteAsync(cancellationToken);
        await db.OperationJobEvents.Where(item => item.OccurredAtUtc < eventCutoff).ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task ApplyRollups(PlatformDbContext db, CancellationToken cancellationToken)
    {
        var end = DateTimeOffset.UtcNow.AddMinutes(-1);
        var start = end.AddHours(-2);
        var samples = await db.MetricSamples.AsNoTracking()
            .Where(item => item.SampledAtUtc >= start && item.SampledAtUtc < end && item.ValueDouble != null)
            .ToArrayAsync(cancellationToken);
        foreach (var group in samples.GroupBy(item => new
                 {
                     item.SeriesId,
                     Window = new DateTimeOffset(item.SampledAtUtc.Year, item.SampledAtUtc.Month, item.SampledAtUtc.Day,
                         item.SampledAtUtc.Hour, item.SampledAtUtc.Minute, 0, TimeSpan.Zero)
                 }))
        {
            if (await db.MetricRollups.AnyAsync(item => item.SeriesId == group.Key.SeriesId &&
                    item.WindowStartUtc == group.Key.Window && item.Resolution == "1m", cancellationToken)) continue;
            var values = group.Select(item => item.ValueDouble!.Value).ToArray();
            db.MetricRollups.Add(new MetricRollup
            {
                SeriesId = group.Key.SeriesId, WindowStartUtc = group.Key.Window, Resolution = "1m",
                Minimum = values.Min(), Maximum = values.Max(), Average = values.Average(), Sum = values.Sum(), SampleCount = values.LongLength
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
