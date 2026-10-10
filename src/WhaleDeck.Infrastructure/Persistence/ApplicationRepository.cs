using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Domain.Entities;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class ApplicationRepository(PlatformDbContext db) : IApplicationRepository
{
    public async Task<IReadOnlyCollection<ApplicationInstallation>> ListAsync(CancellationToken cancellationToken) =>
        await db.ApplicationInstallations.AsNoTracking().OrderBy(item => item.DisplayName).ToArrayAsync(cancellationToken);

    public Task<ApplicationInstallation?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.ApplicationInstallations.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<(ManagedResource Resource, string Role)>> ListResourcesAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var rows = await (from link in db.ApplicationResources.AsNoTracking()
                          join resource in db.ManagedResources.AsNoTracking() on link.ResourceId equals resource.Id
                          where link.ApplicationId == applicationId
                          orderby link.Role, resource.DisplayName
                          select new { Resource = resource, link.Role }).ToArrayAsync(cancellationToken);
        return rows.Select(item => (item.Resource, item.Role)).ToArray();
    }

    public async Task<IReadOnlyCollection<ApplicationUpdateRun>> ListUpdateRunsAsync(
        Guid applicationId,
        int take,
        CancellationToken cancellationToken) =>
        await db.ApplicationUpdateRuns.AsNoTracking().Where(item => item.ApplicationId == applicationId)
            .OrderByDescending(item => item.StartedAtUtc).Take(Math.Clamp(take, 1, 500)).ToArrayAsync(cancellationToken);
}
