using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class JobRepository(PlatformDbContext dbContext) : IJobRepository
{
    public async Task<OperationJob> EnqueueAsync(OperationJob job, OutboxMessage outbox, AuditEvent audit, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OperationJobs.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ActorSubject == job.ActorSubject && item.IdempotencyKey == job.IdempotencyKey, cancellationToken);
        if (existing is not null) return existing;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.OperationJobs.Add(job);
        dbContext.OperationJobEvents.Add(new OperationJobEvent
        {
            JobId = job.Id,
            Sequence = 1,
            State = job.State,
            Phase = job.Phase,
            MessageCode = "JOB_ACCEPTED"
        });
        dbContext.OutboxMessages.Add(outbox);
        dbContext.AuditEvents.Add(audit);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public Task<OperationJob?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.OperationJobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<OperationJob>> ListAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.OperationJobs.AsNoTracking().OrderByDescending(item => item.CreatedAtUtc).Take(take).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<OperationJobEvent>> ListEventsAsync(Guid id, long afterSequence, CancellationToken cancellationToken) =>
        await dbContext.OperationJobEvents.AsNoTracking().Where(item => item.JobId == id && item.Sequence > afterSequence)
            .OrderBy(item => item.Sequence).Take(500).ToArrayAsync(cancellationToken);

    public async Task RequestCancellationAsync(Guid id, string actorSubject, CancellationToken cancellationToken)
    {
        var job = await dbContext.OperationJobs.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Job was not found.");
        if (job.State is "Succeeded" or "Failed" or "RolledBack" or "Canceled")
        {
            throw new InvalidOperationException("Completed jobs cannot be canceled.");
        }
        job.CancelRequestedAtUtc = DateTimeOffset.UtcNow;
        job.Version++;
        dbContext.OutboxMessages.Add(new OutboxMessage { MessageType = "OperationCancellationRequested", PayloadJson = $"{{\"jobId\":\"{id:D}\",\"actor\":\"{actorSubject}\"}}" });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
