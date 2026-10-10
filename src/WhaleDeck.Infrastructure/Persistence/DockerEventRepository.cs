using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class DockerEventRepository(PlatformDbContext db) : IDockerEventRepository
{
    public async Task<IReadOnlyCollection<DockerEventRecord>> ListAsync(
        string? containerIdOrName,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.DockerEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(containerIdOrName))
        {
            if (containerIdOrName.Length > 128 || containerIdOrName.Any(char.IsControl))
                throw new ArgumentException("Container event filter is invalid.");
            query = query.Where(item => item.ResourceId.StartsWith(containerIdOrName) || item.ResourceName == containerIdOrName);
        }
        return await query.OrderByDescending(item => item.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToArrayAsync(cancellationToken);
    }
}
