using Docker.DotNet;
using Docker.DotNet.Models;
using WhaleDeck.Contracts.Agent.V1;

namespace WhaleDeck.Agent.Services;

public sealed class DockerEngine : IDisposable
{
    private readonly DockerClient _client;
    private readonly ResourceRegistry _registry;
    private readonly OperationStore _operations;
    private readonly PlanStore _plans;

    public DockerEngine(ResourceRegistry registry, OperationStore operations, PlanStore plans)
    {
        _registry = registry;
        _operations = operations;
        _plans = plans;
        _client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        await TryAsync(async () => { await _client.System.PingAsync(cancellationToken); });

    public Task<SystemInfoResponse> GetInfoAsync(CancellationToken cancellationToken) =>
        _client.System.GetSystemInfoAsync(cancellationToken);

    public Task<IList<ContainerListResponse>> ListContainersAsync(bool all, CancellationToken cancellationToken) =>
        _client.Containers.ListContainersAsync(new ContainersListParameters { All = all }, cancellationToken);

    public Task<IList<ImagesListResponse>> ListImagesAsync(CancellationToken cancellationToken) =>
        _client.Images.ListImagesAsync(new ImagesListParameters { All = true }, cancellationToken);

    public Task<IList<NetworkResponse>> ListNetworksAsync(CancellationToken cancellationToken) =>
        _client.Networks.ListNetworksAsync(cancellationToken: cancellationToken);

    public async Task<IList<VolumeResponse>> ListVolumesAsync(CancellationToken cancellationToken)
    {
        var response = await _client.Volumes.ListAsync(cancellationToken: cancellationToken);
        return response.Volumes ?? [];
    }

    public PlanResponse Plan(string resourceId, string action, IReadOnlyDictionary<string, string> parameters) =>
        _plans.Create(resourceId, action, parameters);

    public async Task<OperationHandle> ChangeContainerStateAsync(
        string containerId,
        ContainerAction action,
        uint timeoutSeconds,
        bool preserveVolumes,
        string? planHash,
        CancellationToken cancellationToken)
    {
        var containers = await ListContainersAsync(true, cancellationToken);
        var container = containers.FirstOrDefault(item =>
            item.ID.StartsWith(containerId, StringComparison.OrdinalIgnoreCase) ||
            item.Names.Any(name => string.Equals(name.TrimStart('/'), containerId.TrimStart('/'), StringComparison.Ordinal)));
        if (container is null)
        {
            throw new InvalidOperationException("Container was not found.");
        }

        var name = container.Names.FirstOrDefault()?.TrimStart('/') ?? container.ID;
        var protectedResource = _registry.IsProtectedContainer(name, container.Labels);
        if (protectedResource && action is ContainerAction.Stop or ContainerAction.Restart or ContainerAction.Delete)
        {
            throw new InvalidOperationException("Protected containers require the platform maintenance workflow.");
        }
        if (action is ContainerAction.Delete)
        {
            _plans.VerifyAndConsume(planHash ?? string.Empty, containerId, "delete", new Dictionary<string, string>
            {
                ["timeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["preserveVolumes"] = preserveVolumes.ToString().ToLowerInvariant()
            });
        }

        var operation = _operations.Create($"Container{action}");
        operation.State = OperationState.Running;
        operation.ProgressPercent = 25;
        _operations.Save(operation);

        try
        {
            switch (action)
            {
                case ContainerAction.Start:
                    await _client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters(), cancellationToken);
                    break;
                case ContainerAction.Stop:
                    await _client.Containers.StopContainerAsync(container.ID,
                        new ContainerStopParameters { WaitBeforeKillSeconds = timeoutSeconds is 0 ? 10 : timeoutSeconds }, cancellationToken);
                    break;
                case ContainerAction.Restart:
                    await _client.Containers.RestartContainerAsync(container.ID,
                        new ContainerRestartParameters { WaitBeforeKillSeconds = timeoutSeconds is 0 ? 10 : timeoutSeconds }, cancellationToken);
                    break;
                case ContainerAction.Delete:
                    await _client.Containers.RemoveContainerAsync(container.ID,
                        new ContainerRemoveParameters { RemoveVolumes = !preserveVolumes, Force = false }, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported container action.");
            }

            operation.State = OperationState.Succeeded;
            operation.ProgressPercent = 100;
            operation.Phase = "Completed";
        }
        catch
        {
            operation.State = OperationState.Failed;
            operation.ErrorCode = "DOCKER_OPERATION_FAILED";
            operation.Phase = "Failed";
            throw;
        }
        finally
        {
            _operations.Save(operation);
        }

        return operation;
    }

    public void Dispose() => _client.Dispose();

    private static async Task<bool> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (DockerApiException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
