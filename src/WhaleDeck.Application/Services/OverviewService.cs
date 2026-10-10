using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public sealed class OverviewService(PortalService portals, IAgentGateway agent, ICatalogProvider catalog)
{
    public async Task<OverviewDto> GetAsync(string subject, bool administrator, CancellationToken cancellationToken)
    {
        var visiblePortals = await portals.ListAsync(subject, administrator, cancellationToken);
        if (!administrator)
        {
            return new OverviewDto("Public", "Online", DateTimeOffset.UtcNow, visiblePortals, null, null, null, null, null, false);
        }

        var health = await agent.GetHealthAsync(cancellationToken);
        var host = health.Available ? await agent.GetHostInfoAsync(cancellationToken) : null;
        var resources = health.Available ? await agent.ListResourcesAsync("overview", cancellationToken) : [];
        var metrics = health.Available ? await agent.GetMetricsSnapshotAsync(cancellationToken) : [];
        var popular = await catalog.GetPopularAsync(cancellationToken);
        return new OverviewDto("Administrator", health.Status, health.ObservedAtUtc, visiblePortals, health, host, resources,
            metrics, popular.Applications, popular.IsStale);
    }
}
