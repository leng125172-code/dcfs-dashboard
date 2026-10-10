using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public sealed class ContainerUpdateService(IContainerUpdateRepository repository)
{
    public async Task<IReadOnlyCollection<ContainerUpdateRunDto>> ListAsync(
        string? containerIdOrName,
        int take,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(containerIdOrName, Math.Clamp(take, 1, 500), cancellationToken))
        .Select(item => new ContainerUpdateRunDto(item.Id, item.ExternalContainerId, item.ContainerName, item.Image,
            item.OldImageDigest, item.NewImageDigest, item.VersionPolicy, item.JobId, item.StartedAtUtc,
            item.CompletedAtUtc, item.Result, item.WasRolledBack, item.ErrorSummary))
        .ToArray();
}
