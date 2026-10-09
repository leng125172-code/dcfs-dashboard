using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
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
        await CollectMetrics(db, agent, cancellationToken);
        await DispatchOutbox(db, agent, cancellationToken);

        if (_nextCatalogRefresh <= DateTimeOffset.UtcNow)
        {
            await scope.ServiceProvider.GetRequiredService<ICatalogProvider>().GetPopularAsync(cancellationToken);
            _nextCatalogRefresh = DateTimeOffset.UtcNow.AddHours(1);
        }
        if (_nextRetentionSweep <= DateTimeOffset.UtcNow)
        {
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
        foreach (var metric in await agent.GetMetricsSnapshotAsync(cancellationToken))
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

    private static async Task DispatchOutbox(PlatformDbContext db, IAgentGateway agent, CancellationToken cancellationToken)
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
                        job.State = "Running";
                        job.Phase = "DispatchingToAgent";
                        job.StartedAtUtc ??= DateTimeOffset.UtcNow;
                        job.Version++;
                        await db.SaveChangesAsync(cancellationToken);
                        var operation = await agent.ExecuteAsync(split[0], split[1], resourceId, parameters, planHash, cancellationToken);
                        job.AgentOperationId = Guid.TryParse(operation.OperationId, out var operationId) ? operationId : null;
                        await Complete(db, job, operation.State.Contains("Succeeded", StringComparison.OrdinalIgnoreCase) ? "Succeeded" : "Waiting", operation.Phase, operation.ErrorCode, cancellationToken);
                    }
                }
                message.ProcessedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                message.Attempts++;
                message.LastErrorCode = "OUTBOX_DISPATCH_FAILED";
                message.AvailableAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, message.Attempts)));
                await db.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
    }

    private static async Task Complete(PlatformDbContext db, OperationJob job, string state, string phase, string? errorCode, CancellationToken cancellationToken)
    {
        job.State = state;
        job.Phase = phase;
        job.ErrorCode = errorCode;
        job.ProgressPercent = state == "Succeeded" ? (short)100 : null;
        if (state is "Succeeded" or "Failed" or "Canceled") job.CompletedAtUtc = DateTimeOffset.UtcNow;
        job.Version++;
        var sequence = await db.OperationJobEvents.Where(item => item.JobId == job.Id).MaxAsync(item => (long?)item.Sequence, cancellationToken) ?? 0;
        db.OperationJobEvents.Add(new OperationJobEvent { JobId = job.Id, Sequence = sequence + 1, State = state, Phase = phase, ProgressPercent = job.ProgressPercent, MessageCode = $"JOB_{state.ToUpperInvariant()}" });
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
}
