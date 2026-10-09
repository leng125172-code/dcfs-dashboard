using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using WhaleDeck.Contracts.Agent.V1;

namespace WhaleDeck.Agent.Services;

public sealed class DockerGrpcService(DockerEngine docker, ResourceRegistry registry) : DockerService.DockerServiceBase
{
    public override async Task<DockerInfoResponse> GetEngineInfo(Empty request, ServerCallContext context)
    {
        var info = await docker.GetInfoAsync(context.CancellationToken);
        return new DockerInfoResponse
        {
            ServerVersion = info.ServerVersion ?? string.Empty,
            OperatingSystem = info.OperatingSystem ?? string.Empty,
            Architecture = info.Architecture ?? string.Empty,
            Containers = checked((int)info.Containers),
            Images = checked((int)info.Images),
            LoggingDriver = info.LoggingDriver ?? string.Empty
        };
    }

    public override async Task<ListContainersResponse> ListContainers(ListContainersRequest request, ServerCallContext context)
    {
        var containers = await docker.ListContainersAsync(request.IncludeStopped, context.CancellationToken);
        var response = new ListContainersResponse();
        foreach (var container in containers)
        {
            var name = container.Names.FirstOrDefault()?.TrimStart('/') ?? container.ID[..Math.Min(12, container.ID.Length)];
            var protectedResource = registry.IsProtectedContainer(name, container.Labels);
            var protectionLevel = container.Labels is not null && container.Labels.TryGetValue("io.whaledeck.protection-level", out var configuredLevel)
                ? configuredLevel
                : protectedResource ? "CriticalData" : "Managed";
            var summary = new ContainerSummary
            {
                Id = container.ID,
                Name = name,
                Image = container.Image,
                State = container.State,
                Status = container.Status,
                ProtectedResource = protectedResource,
                ProtectionLevel = protectionLevel
            };
            if (container.Labels is not null)
            {
                foreach (var label in container.Labels.Where(item => !IsSensitiveLabel(item.Key)))
                {
                    summary.Labels[label.Key] = label.Value;
                }
            }
            response.Containers.Add(summary);
        }
        return response;
    }

    public override Task<OperationHandle> ChangeContainerState(ChangeContainerStateRequest request, ServerCallContext context) =>
        docker.ChangeContainerStateAsync(
            request.ContainerId,
            request.Action,
            request.TimeoutSeconds,
            request.PreserveVolumes,
            request.PlanHash,
            request.Context?.JobId ?? string.Empty,
            request.Context?.IdempotencyKey ?? string.Empty,
            context.CancellationToken);

    public override async Task<ResourceCollectionResponse> ListImages(Empty request, ServerCallContext context)
    {
        var response = new ResourceCollectionResponse();
        foreach (var image in await docker.ListImagesAsync(context.CancellationToken))
        {
            var snapshot = new ResourceSnapshot
            {
                ResourceId = $"image:{image.ID}",
                DisplayName = image.RepoTags?.FirstOrDefault() ?? image.ID,
                ResourceType = "Image",
                State = image.Containers > 0 ? "InUse" : "Unused",
                Version = image.ID
            };
            snapshot.Attributes["sizeBytes"] = image.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);
            response.Resources.Add(snapshot);
        }
        return response;
    }

    public override async Task<ResourceCollectionResponse> ListNetworks(Empty request, ServerCallContext context)
    {
        var response = new ResourceCollectionResponse();
        foreach (var network in await docker.ListNetworksAsync(context.CancellationToken))
        {
            var snapshot = new ResourceSnapshot
            {
                ResourceId = $"network:{network.ID}",
                DisplayName = network.Name,
                ResourceType = "DockerNetwork",
                State = network.Internal ? "Internal" : "External",
                Version = network.Driver
            };
            snapshot.Attributes["scope"] = network.Scope;
            response.Resources.Add(snapshot);
        }
        return response;
    }

    public override async Task<ResourceCollectionResponse> ListVolumes(Empty request, ServerCallContext context)
    {
        var response = new ResourceCollectionResponse();
        foreach (var volume in await docker.ListVolumesAsync(context.CancellationToken))
        {
            var snapshot = new ResourceSnapshot
            {
                ResourceId = $"volume:{volume.Name}",
                DisplayName = volume.Name,
                ResourceType = "DockerVolume",
                State = "Available",
                Version = volume.Driver
            };
            response.Resources.Add(snapshot);
        }
        return response;
    }

    public override Task<PlanResponse> PlanAction(RegisteredActionRequest request, ServerCallContext context) =>
        Task.FromResult(docker.Plan(request.Resource?.ResourceId ?? string.Empty, request.Action, request.Parameters));

    private static bool IsSensitiveLabel(string key) =>
        key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("secret", StringComparison.OrdinalIgnoreCase);
}
