using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Contracts.Agent.V1;

namespace WhaleDeck.Infrastructure.Agent;

public sealed class AgentGateway : IAgentGateway, IManagementQuery, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly AgentService.AgentServiceClient _agent;
    private readonly HostService.HostServiceClient _host;
    private readonly DockerService.DockerServiceClient _docker;
    private readonly ManagedResourceService.ManagedResourceServiceClient _resources;
    private readonly OperationService.OperationServiceClient _operations;
    private readonly byte[]? _capabilityKey;

    public AgentGateway(IConfiguration configuration)
    {
        var socketPath = configuration["Agent:SocketPath"] ?? "/run/whaledeck/agent.sock";
        var keyFile = configuration["Agent:CapabilityKeyFile"] ?? "/run/secrets/agent-capability.key";
        _capabilityKey = File.Exists(keyFile) ? File.ReadAllBytes(keyFile) : null;
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
        _channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });
        _agent = new AgentService.AgentServiceClient(_channel);
        _host = new HostService.HostServiceClient(_channel);
        _docker = new DockerService.DockerServiceClient(_channel);
        _resources = new ManagedResourceService.ManagedResourceServiceClient(_channel);
        _operations = new OperationService.OperationServiceClient(_channel);
    }

    public async Task<AgentHealthDto> GetHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _agent.GetHealthAsync(new Empty(), cancellationToken: cancellationToken);
            return new AgentHealthDto(response.Healthy, response.DockerAvailable, response.SystemdAvailable, response.Status, response.ObservedAtUtc.ToDateTimeOffset());
        }
        catch (RpcException exception) when (exception.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            return new AgentHealthDto(false, false, false, "AgentUnavailable", DateTimeOffset.UtcNow);
        }
    }

    public async Task<HostInfoDto> GetHostInfoAsync(CancellationToken cancellationToken)
    {
        var response = await _host.GetHostInfoAsync(new Empty(), Headers("/whaledeck.agent.v1.HostService/GetHostInfo"), cancellationToken: cancellationToken);
        return new HostInfoDto(response.HostName, response.Distribution, response.KernelVersion, response.Architecture,
            DateTimeOffset.FromUnixTimeSeconds(response.BootTimeUnixSeconds), TimeSpan.FromSeconds(response.UptimeSeconds),
            checked((int)response.LogicalProcessorCount), response.TotalMemoryBytes);
    }

    public async Task<IReadOnlyCollection<ContainerDto>> ListContainersAsync(bool includeStopped, CancellationToken cancellationToken)
    {
        var response = await _docker.ListContainersAsync(new ListContainersRequest { IncludeStopped = includeStopped }, Headers("/whaledeck.agent.v1.DockerService/ListContainers"), cancellationToken: cancellationToken);
        return response.Containers.Select(item => new ContainerDto(item.Id, item.Name, item.Image, item.State, item.Status,
            item.ProtectedResource, item.ProtectionLevel, item.Labels)).ToArray();
    }

    public async Task<IReadOnlyCollection<ManagedResourceDto>> ListResourcesAsync(string kind, CancellationToken cancellationToken)
    {
        return kind.ToLowerInvariant() switch
        {
            "databases" => Map(await _resources.ListDatabaseInstancesAsync(new Empty(), Headers("/whaledeck.agent.v1.ManagedResourceService/ListDatabaseInstances"), cancellationToken: cancellationToken)),
            "systemd" => Map(await _resources.ListSystemdUnitsAsync(new Empty(), Headers("/whaledeck.agent.v1.ManagedResourceService/ListSystemdUnits"), cancellationToken: cancellationToken)),
            "compose-projects" or "applications" => Map(await _resources.ListComposeProjectsAsync(new Empty(), Headers("/whaledeck.agent.v1.ManagedResourceService/ListComposeProjects"), cancellationToken: cancellationToken)),
            "images" => Map(await _docker.ListImagesAsync(new Empty(), Headers("/whaledeck.agent.v1.DockerService/ListImages"), cancellationToken: cancellationToken)),
            "networks" => Map(await _docker.ListNetworksAsync(new Empty(), Headers("/whaledeck.agent.v1.DockerService/ListNetworks"), cancellationToken: cancellationToken)),
            "volumes" => Map(await _docker.ListVolumesAsync(new Empty(), Headers("/whaledeck.agent.v1.DockerService/ListVolumes"), cancellationToken: cancellationToken)),
            "overview" => (await ListContainersAsync(true, cancellationToken)).Select(item => new ManagedResourceDto(item.Id, item.Name, "Container", item.State, item.Image, item.IsProtected, item.Labels)).ToArray(),
            _ => []
        };
    }

    public async Task<PlanDto> PlanAsync(string area, string action, string? resourceId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var normalizedParameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        if (area.Equals("containers", StringComparison.OrdinalIgnoreCase) && action.Equals("delete", StringComparison.OrdinalIgnoreCase))
        {
            normalizedParameters["timeoutSeconds"] = parameters.TryGetValue("timeoutSeconds", out var timeout) && uint.TryParse(timeout, out var seconds)
                ? seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "10";
            normalizedParameters["preserveVolumes"] = (!parameters.TryGetValue("preserveVolumes", out var preserve) || !bool.TryParse(preserve, out var preserveValue) || preserveValue)
                .ToString().ToLowerInvariant();
        }
        var request = new RegisteredActionRequest
        {
            Action = action,
            Resource = new ResourceReference { ResourceId = resourceId ?? string.Empty, ResourceType = area }
        };
        request.Parameters.Add(normalizedParameters);
        var response = await _docker.PlanActionAsync(request, Headers("/whaledeck.agent.v1.DockerService/PlanAction"), cancellationToken: cancellationToken);
        return new PlanDto(response.PlanHash, response.ExpiresAtUtc.ToDateTimeOffset(), response.Changes, response.Warnings);
    }

    public async Task<IReadOnlyCollection<MetricValueDto>> GetMetricsSnapshotAsync(CancellationToken cancellationToken)
    {
        using var call = _host.StreamMetrics(new MetricsRequest { IntervalSeconds = 6 },
            Headers("/whaledeck.agent.v1.HostService/StreamMetrics"), cancellationToken: cancellationToken);
        var values = new List<MetricValueDto>(2);
        while (values.Count < 2 && await call.ResponseStream.MoveNext(cancellationToken))
        {
            var item = call.ResponseStream.Current;
            values.Add(new MetricValueDto(item.MetricKind, item.DeviceId, item.Value, item.Unit, item.Quality, item.SampledAtUtc.ToDateTimeOffset()));
        }
        return values;
    }

    public async Task<AgentOperationDto> ExecuteAsync(string area, string action, string resourceId, IReadOnlyDictionary<string, string> parameters, string? planHash, CancellationToken cancellationToken)
    {
        OperationHandle response;
        if (area.Equals("containers", StringComparison.OrdinalIgnoreCase) && System.Enum.TryParse<ContainerAction>(action, true, out var containerAction))
        {
            response = await _docker.ChangeContainerStateAsync(new ChangeContainerStateRequest
            {
                ContainerId = resourceId,
                Action = containerAction,
                TimeoutSeconds = parameters.TryGetValue("timeoutSeconds", out var timeout) && uint.TryParse(timeout, out var seconds) ? seconds : 10,
                PreserveVolumes = !parameters.TryGetValue("preserveVolumes", out var preserve) || !bool.TryParse(preserve, out var preserveValue) || preserveValue,
                PlanHash = planHash ?? string.Empty
            }, Headers("/whaledeck.agent.v1.DockerService/ChangeContainerState"), cancellationToken: cancellationToken);
        }
        else
        {
            var request = new RegisteredActionRequest
            {
                Action = action,
                Resource = new ResourceReference { ResourceId = resourceId, ResourceType = area },
                PlanHash = planHash ?? string.Empty
            };
            request.Parameters.Add(parameters.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
            response = area.ToLowerInvariant() switch
            {
                "databases" or "backups" => await _resources.RunDatabaseActionAsync(request, Headers("/whaledeck.agent.v1.ManagedResourceService/RunDatabaseAction"), cancellationToken: cancellationToken),
                "host" => await _resources.RunSystemdActionAsync(request, Headers("/whaledeck.agent.v1.ManagedResourceService/RunSystemdAction"), cancellationToken: cancellationToken),
                "applications" => await _resources.RunComposeActionAsync(request, Headers("/whaledeck.agent.v1.ManagedResourceService/RunComposeAction"), cancellationToken: cancellationToken),
                "config-repository" => await _resources.RunConfigRepositoryActionAsync(request, Headers("/whaledeck.agent.v1.ManagedResourceService/RunConfigRepositoryAction"), cancellationToken: cancellationToken),
                "platform" or "docker" => await _resources.RunPlatformMaintenanceAsync(request, Headers("/whaledeck.agent.v1.ManagedResourceService/RunPlatformMaintenance"), cancellationToken: cancellationToken),
                _ => throw new InvalidOperationException("The Agent does not support this operation area.")
            };
        }

        return new AgentOperationDto(response.OperationId, response.State.ToString(), response.Phase, response.ProgressPercent,
            string.IsNullOrWhiteSpace(response.ErrorCode) ? null : response.ErrorCode);
    }

    public async Task<AgentOperationDto> GetOperationAsync(string operationId, CancellationToken cancellationToken)
    {
        var response = await _operations.GetOperationAsync(
            new OperationRequest { OperationId = operationId },
            Headers("/whaledeck.agent.v1.OperationService/GetOperation"),
            cancellationToken: cancellationToken);
        return Map(response);
    }

    public async Task<AgentOperationDto> CancelOperationAsync(string operationId, CancellationToken cancellationToken)
    {
        var request = new OperationRequest { OperationId = operationId };
        try
        {
            var response = await _operations.CancelOperationAsync(
                request,
                Headers("/whaledeck.agent.v1.OperationService/CancelOperation"),
                cancellationToken: cancellationToken);
            return Map(response);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.FailedPrecondition)
        {
            var response = await _operations.GetOperationAsync(
                request,
                Headers("/whaledeck.agent.v1.OperationService/GetOperation"),
                cancellationToken: cancellationToken);
            return Map(response);
        }
    }

    public Task<IReadOnlyCollection<ManagedResourceDto>> ListAsync(string area, CancellationToken cancellationToken) =>
        ListResourcesAsync(area, cancellationToken);

    public void Dispose() => _channel.Dispose();

    private Metadata Headers(string method)
    {
        if (_capabilityKey is null) return [];
        var expires = DateTimeOffset.UtcNow.AddSeconds(45).ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");
        using var hmac = new HMACSHA256(_capabilityKey);
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{expires}.{nonce}.{method}")));
        return new Metadata { { "x-whaledeck-capability", $"{expires}.{nonce}.{signature}" } };
    }

    private static ManagedResourceDto[] Map(ResourceCollectionResponse response) =>
        response.Resources.Select(item => new ManagedResourceDto(item.ResourceId, item.DisplayName, item.ResourceType,
            item.State, item.Version, item.ProtectedResource, item.Attributes)).ToArray();

    private static AgentOperationDto Map(OperationHandle response) =>
        new(response.OperationId, response.State.ToString(), response.Phase, response.ProgressPercent,
            string.IsNullOrWhiteSpace(response.ErrorCode) ? null : response.ErrorCode);
}
