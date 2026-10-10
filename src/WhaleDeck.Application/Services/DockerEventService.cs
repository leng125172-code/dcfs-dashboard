using System.Text.Json;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public sealed class DockerEventService(IDockerEventRepository repository)
{
    public async Task<IReadOnlyCollection<DockerEventDto>> ListAsync(
        string? containerIdOrName,
        int take,
        CancellationToken cancellationToken) =>
        (await repository.ListAsync(containerIdOrName, take, cancellationToken))
        .Select(item => new DockerEventDto(
            item.Fingerprint,
            item.OccurredAtUtc,
            item.EventType,
            item.Action,
            item.ResourceId,
            item.ResourceName,
            item.Image,
            JsonSerializer.Deserialize<Dictionary<string, string>>(item.AttributesJson) ?? []))
        .ToArray();
}
