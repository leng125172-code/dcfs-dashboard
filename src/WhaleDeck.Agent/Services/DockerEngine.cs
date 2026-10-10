using Docker.DotNet;
using Docker.DotNet.Models;
using WhaleDeck.Contracts.Agent.V1;
using System.Text.RegularExpressions;
using System.Net;

namespace WhaleDeck.Agent.Services;

public sealed class DockerEngine : IDisposable
{
    private static readonly Regex ContainerNamePattern = new("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex ImagePattern = new("^[a-zA-Z0-9][a-zA-Z0-9./_:@-]{0,254}$", RegexOptions.CultureInvariant);
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

    public async Task<ImageInspectResponse> InspectImageAsync(string image, bool pullIfMissing, CancellationToken cancellationToken)
    {
        ValidateImage(image);
        try
        {
            return await _client.Images.InspectImageAsync(image, cancellationToken);
        }
        catch (DockerApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound && pullIfMissing)
        {
            await PullImageAsync(image, cancellationToken);
            return await _client.Images.InspectImageAsync(image, cancellationToken);
        }
    }

    public Task<IList<NetworkResponse>> ListNetworksAsync(CancellationToken cancellationToken) =>
        _client.Networks.ListNetworksAsync(cancellationToken: cancellationToken);

    public async Task<IList<VolumeResponse>> ListVolumesAsync(CancellationToken cancellationToken)
    {
        var response = await _client.Volumes.ListAsync(cancellationToken: cancellationToken);
        return response.Volumes ?? [];
    }

    public PlanResponse Plan(string resourceId, string action, IReadOnlyDictionary<string, string> parameters) =>
        _plans.Create(resourceId, action, parameters);

    public async Task<OperationHandle> RunActionAsync(
        string resourceId,
        string action,
        IReadOnlyDictionary<string, string> parameters,
        string? planHash,
        string jobId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var operationKey = string.IsNullOrWhiteSpace(jobId)
            ? string.Empty
            : $"{jobId}:{idempotencyKey}:Docker:{action}:{resourceId}";
        var operation = _operations.GetOrCreate(operationKey, $"Docker{action}", out var created);
        if (!created) return operation;
        operation.State = OperationState.Running;
        operation.Phase = "Validating";
        operation.ProgressPercent = 10;
        _operations.Save(operation);

        try
        {
            if (action is "create" or "prune") _plans.VerifyAndConsume(planHash ?? string.Empty, resourceId, action, parameters);
            switch (action)
            {
                case "pull":
                    await PullImageAsync(Required(parameters, "image"), cancellationToken);
                    break;
                case "create":
                    await CreateManagedContainerAsync(parameters, cancellationToken);
                    break;
                case "prune":
                    await PruneAsync(parameters, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported Docker action.");
            }
            operation.State = OperationState.Succeeded;
            operation.Phase = "Completed";
            operation.ProgressPercent = 100;
        }
        catch
        {
            operation.State = OperationState.Failed;
            operation.Phase = "Failed";
            operation.ErrorCode = "DOCKER_OPERATION_FAILED";
            throw;
        }
        finally
        {
            _operations.Save(operation);
        }
        return operation;
    }

    private async Task PullImageAsync(string image, CancellationToken cancellationToken)
    {
        ValidateImage(image);
        var (name, tag) = SplitImage(image);
        await _client.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = name, Tag = tag },
            new AuthConfig(), new Progress<JSONMessage>(), cancellationToken);
    }

    private async Task CreateManagedContainerAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var image = Required(parameters, "image");
        var name = Required(parameters, "name");
        ValidateImage(image);
        if (!ContainerNamePattern.IsMatch(name)) throw new InvalidOperationException("Container name is invalid.");
        if (name.StartsWith("database-platform-", StringComparison.OrdinalIgnoreCase) || name.StartsWith("whaledeck-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reserved container name prefix.");

        var memoryMb = ParseLong(parameters, "memoryMb", 512, 32, 32768);
        var cpus = ParseDouble(parameters, "cpus", 1, 0.1, 32);
        var autoUpdate = parameters.TryGetValue("autoUpdate", out var configured) && bool.TryParse(configured, out var enabled) && enabled;
        await PullImageAsync(image, cancellationToken);
        var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = name,
            Image = image,
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["io.whaledeck.managed"] = "true",
                ["io.whaledeck.protected"] = "false",
                ["io.whaledeck.autoupdate"] = autoUpdate ? "true" : "false"
            },
            HostConfig = new HostConfig
            {
                CapDrop = ["ALL"],
                SecurityOpt = ["no-new-privileges:true"],
                Init = true,
                Memory = memoryMb * 1024 * 1024,
                NanoCPUs = checked((long)(cpus * 1_000_000_000)),
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped }
            }
        }, cancellationToken);
        if (response.Warnings is { Count: > 0 }) throw new InvalidOperationException("Docker returned warnings while creating the container.");
        await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), cancellationToken);
    }

    private async Task PruneAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var pruneImages = !parameters.TryGetValue("images", out var images) || !bool.TryParse(images, out var parsedImages) || parsedImages;
        var pruneVolumes = parameters.TryGetValue("volumes", out var volumes) && bool.TryParse(volumes, out var parsedVolumes) && parsedVolumes;
        if (pruneImages)
        {
            await _client.Images.PruneImagesAsync(new ImagesPruneParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>> { ["dangling"] = new Dictionary<string, bool> { ["true"] = true } }
            }, cancellationToken);
        }
        if (pruneVolumes)
        {
            await _client.Volumes.PruneAsync(new VolumesPruneParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>> { ["label!"] = new Dictionary<string, bool> { ["io.whaledeck.protected=true"] = true } }
            }, cancellationToken);
        }
    }

    public async Task<OperationHandle> ChangeContainerStateAsync(
        string containerId,
        ContainerAction action,
        uint timeoutSeconds,
        bool preserveVolumes,
        string? planHash,
        string jobId,
        string idempotencyKey,
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
        var operationKey = string.IsNullOrWhiteSpace(jobId)
            ? string.Empty
            : $"{jobId}:{idempotencyKey}:Container:{action}:{container.ID}";
        var operation = _operations.GetOrCreate(operationKey, $"Container{action}", out var created);
        if (!created) return operation;
        operation.State = OperationState.Running;
        operation.ProgressPercent = 25;
        _operations.Save(operation);

        try
        {
            if (action is ContainerAction.Delete)
            {
                _plans.VerifyAndConsume(planHash ?? string.Empty, containerId, "delete", new Dictionary<string, string>
                {
                    ["timeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["preserveVolumes"] = preserveVolumes.ToString().ToLowerInvariant()
                });
            }

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

    private static string Required(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"Required Docker parameter is missing: {key}");

    private static void ValidateImage(string image)
    {
        if (!ImagePattern.IsMatch(image) || image.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Image reference is invalid.");
    }

    private static (string Name, string Tag) SplitImage(string image)
    {
        if (image.Contains('@', StringComparison.Ordinal)) return (image, string.Empty);
        var slash = image.LastIndexOf('/');
        var colon = image.LastIndexOf(':');
        return colon > slash ? (image[..colon], image[(colon + 1)..]) : (image, "latest");
    }

    private static long ParseLong(IReadOnlyDictionary<string, string> parameters, string key, long fallback, long minimum, long maximum)
    {
        if (!parameters.TryGetValue(key, out var value)) return fallback;
        return long.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed : throw new InvalidOperationException($"Docker parameter is out of range: {key}");
    }

    private static double ParseDouble(IReadOnlyDictionary<string, string> parameters, string key, double fallback, double minimum, double maximum)
    {
        if (!parameters.TryGetValue(key, out var value)) return fallback;
        return double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed : throw new InvalidOperationException($"Docker parameter is out of range: {key}");
    }

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
