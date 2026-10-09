using Microsoft.EntityFrameworkCore;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class ResourceLeaseManager(PlatformDbContext dbContext)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    public async Task<bool> TryAcquireAsync(string lockKey, Guid jobId, string ownerId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(LeaseDuration);
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO resource_operation_leases
                (lock_key, job_id, owner_id, acquired_at_utc, lease_expires_at_utc, version)
            VALUES
                ({lockKey}, {jobId}, {ownerId}, {now}, {expires}, 0)
            ON CONFLICT (lock_key) DO UPDATE SET
                job_id = EXCLUDED.job_id,
                owner_id = EXCLUDED.owner_id,
                acquired_at_utc = CASE
                    WHEN resource_operation_leases.job_id = EXCLUDED.job_id
                    THEN resource_operation_leases.acquired_at_utc
                    ELSE EXCLUDED.acquired_at_utc
                END,
                lease_expires_at_utc = EXCLUDED.lease_expires_at_utc,
                version = resource_operation_leases.version + 1
            WHERE resource_operation_leases.lease_expires_at_utc <= {now}
               OR (resource_operation_leases.job_id = {jobId} AND resource_operation_leases.owner_id = {ownerId});
            """, cancellationToken);
        return affected == 1;
    }

    public Task ReleaseAsync(string lockKey, Guid jobId, string ownerId, CancellationToken cancellationToken) =>
        dbContext.ResourceOperationLeases
            .Where(item => item.LockKey == lockKey && item.JobId == jobId && item.OwnerId == ownerId)
            .ExecuteDeleteAsync(cancellationToken);
}
