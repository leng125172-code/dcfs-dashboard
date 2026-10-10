using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class ContainerUpdateRepository(PlatformDbContext db) : IContainerUpdateRepository
{
    public async Task<IReadOnlyCollection<ContainerUpdateRun>> ListAsync(
        string? containerIdOrName,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.ContainerUpdateRuns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(containerIdOrName))
        {
            var value = containerIdOrName.Trim();
            query = query.Where(item => item.ExternalContainerId == value || item.ContainerName == value);
        }
        return await query.OrderByDescending(item => item.StartedAtUtc).Take(take).ToArrayAsync(cancellationToken);
    }
}
