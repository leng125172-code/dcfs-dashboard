using Docker.DotNet;
using Docker.DotNet.Models;
using WhaleDeck.Contracts.Agent.V1;
using System.Text.RegularExpressions;
using System.Net;
using System.Text.Json;

namespace WhaleDeck.Agent.Services;

public sealed class DockerEngine : IDisposable
{
    private static readonly Regex ContainerNamePattern = new("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex ImagePattern = new("^[a-zA-Z0-9][a-zA-Z0-9./_:@-]{0,254}$", RegexOptions.CultureInvariant);
    private static readonly Regex EnvironmentNamePattern = new("^[A-Z_][A-Z0-9_]{0,127}$", RegexOptions.CultureInvariant);
    private static readonly Regex PortPattern = new("^(0\\.0\\.0\\.0|127\\.0\\.0\\.1):[1-9][0-9]{0,4}:[1-9][0-9]{0,4}(/(tcp|udp))?$", RegexOptions.CultureInvariant);
    private static readonly Regex VolumeNamePattern = new("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.CultureInvariant);
    private static readonly Regex NetworkNamePattern = new("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.CultureInvariant);
    private static readonly Regex LabelNamePattern = new("^[a-zA-Z0-9][a-zA-Z0-9./_-]{0,127}$", RegexOptions.CultureInvariant);
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

    public async Task<Docker.DotNet.Models.ContainerInspectResponse> InspectContainerAsync(string containerId, CancellationToken cancellationToken)
    {
        var container = await ResolveContainerAsync(containerId, cancellationToken);
        return await _client.Containers.InspectContainerAsync(container.ID, cancellationToken);
    }

    public async Task<bool> IsApplicationHealthyAsync(string slug, CancellationToken cancellationToken)
    {
        var expectedName = "whaledeck-app-" + slug;
        var containers = await ListContainersAsync(true, cancellationToken);
        var members = containers.Where(item => item.Names.Any(name =>
            string.Equals(name.TrimStart('/'), expectedName, StringComparison.Ordinal))).ToArray();
        return members.Length > 0 && members.All(item =>
            string.Equals(item.State, "running", StringComparison.OrdinalIgnoreCase) &&
            !(item.Status ?? string.Empty).Contains("unhealthy", StringComparison.OrdinalIgnoreCase));
    }

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

    public async Task<PlanResponse> PlanAsync(
        string resourceId,
        string action,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        ContainerConfiguration? createConfiguration = null;
        string? deleteName = null;
        long? unusedImageBytes = null;
        if (action == "create")
        {
            createConfiguration = ParseContainerConfiguration(parameters);
            await ValidateContainerCreationAsync(createConfiguration, cancellationToken);
        }
        else if (action == "delete")
        {
            var container = await ResolveContainerAsync(resourceId, cancellationToken);
            deleteName = container.Names.FirstOrDefault()?.TrimStart('/') ?? container.ID;
            if (_registry.IsProtectedContainer(deleteName, container.Labels))
                throw new InvalidOperationException("Protected containers require the platform maintenance workflow.");
        }
        else if (action == "prune")
        {
            unusedImageBytes = (await ListImagesAsync(cancellationToken)).Where(item => item.Containers == 0).Sum(item => item.Size);
        }

        // Persist the confirmation token only after every preflight check has passed.
        // This prevents rejected requests from leaving usable-but-invalid plan files behind.
        var response = _plans.Create(resourceId, action, parameters);
        if (createConfiguration is not null)
        {
            response.Changes.Clear();
            response.Changes.Add($"创建容器 {createConfiguration.Name}");
            response.Changes.Add($"使用镜像 {createConfiguration.Image}");
            if (createConfiguration.Ports.Count > 0) response.Changes.Add($"发布 {createConfiguration.Ports.Count} 个端口");
            if (createConfiguration.Mounts.Count > 0) response.Changes.Add($"挂载 {createConfiguration.Mounts.Count} 个命名卷");
            if (createConfiguration.Networks.Length > 0) response.Changes.Add($"连接网络：{string.Join(", ", createConfiguration.Networks)}");
            if (createConfiguration.AutoUpdate) response.Warnings.Add("此容器已允许进入自动镜像更新计划。");
        }
        else if (deleteName is not null)
        {
            response.Changes.Clear();
            response.Changes.Add($"删除容器 {deleteName}");
            response.Warnings.Add(parameters.TryGetValue("preserveVolumes", out var preserve) && preserve == "false"
                ? "匿名卷将随容器删除；命名卷仍会保留。"
                : "数据卷与镜像将保留。");
        }
        else if (unusedImageBytes.HasValue)
        {
            response.Changes.Clear();
            response.Changes.Add($"清理未使用镜像，预计最多回收 {unusedImageBytes.Value} 字节");
        }
        return response;
    }

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
            if (action is "create" or "prune")
                _plans.VerifyAndConsume(planHash ?? string.Empty, resourceId, action, NormalizePlanParameters(parameters));
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
        var configuration = ParseContainerConfiguration(parameters);
        await ValidateContainerCreationAsync(configuration, cancellationToken);
        await PullImageAsync(configuration.Image, cancellationToken);
        string? createdId = null;
        try
        {
            var labels = new Dictionary<string, string>(configuration.Labels, StringComparer.Ordinal)
            {
                ["io.whaledeck.managed"] = "true",
                ["io.whaledeck.protected"] = "false",
                ["io.whaledeck.autoupdate"] = configuration.AutoUpdate ? "true" : "false"
            };
            var response = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Name = configuration.Name,
                Image = configuration.Image,
                Env = configuration.Environment,
                Cmd = configuration.Command,
                Entrypoint = configuration.Entrypoint,
                Labels = labels,
                ExposedPorts = configuration.Ports.ToDictionary(item => item.Key, _ => new EmptyStruct()),
                Healthcheck = configuration.Healthcheck,
                HostConfig = new HostConfig
                {
                    CapDrop = ["ALL"],
                    SecurityOpt = ["no-new-privileges:true"],
                    Init = true,
                    Memory = configuration.MemoryMb * 1024 * 1024,
                    NanoCPUs = checked((long)(configuration.Cpus * 1_000_000_000)),
                    RestartPolicy = new RestartPolicy { Name = configuration.RestartPolicy },
                    NetworkMode = configuration.Networks.FirstOrDefault() ?? "bridge",
                    PortBindings = configuration.Ports,
                    Mounts = configuration.Mounts,
                    LogConfig = new LogConfig
                    {
                        Type = "local",
                        Config = new Dictionary<string, string>
                        {
                            ["max-size"] = "10m",
                            ["max-file"] = "5",
                            ["compress"] = "true"
                        }
                    }
                }
            }, cancellationToken);
            createdId = response.ID;
            if (response.Warnings is { Count: > 0 }) throw new InvalidOperationException("Docker returned warnings while creating the container.");
            foreach (var network in configuration.Networks.Skip(1))
            {
                await _client.Networks.ConnectNetworkAsync(network, new NetworkConnectParameters
                {
                    Container = response.ID,
                    EndpointConfig = new EndpointSettings()
                }, cancellationToken);
            }
            await _client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), cancellationToken);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(createdId))
            {
                try
                {
                    await _client.Containers.RemoveContainerAsync(createdId,
                        new ContainerRemoveParameters { Force = true, RemoveVolumes = false }, CancellationToken.None);
                }
                catch (DockerApiException)
                {
                    // The original create failure remains the actionable error.
                }
            }
            throw;
        }
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

    private async Task ValidateContainerCreationAsync(ContainerConfiguration configuration, CancellationToken cancellationToken)
    {
        var containers = await ListContainersAsync(true, cancellationToken);
        if (containers.Any(item => item.Names.Any(name => string.Equals(name.TrimStart('/'), configuration.Name, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("A container with the requested name already exists.");
        var requestedHostPorts = configuration.Ports.Values.SelectMany(item => item)
            .Select(item => ushort.Parse(item.HostPort, System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
        if (containers.SelectMany(item => item.Ports ?? []).Any(item =>
                item.PublicPort is > 0 and <= ushort.MaxValue && requestedHostPorts.Contains((ushort)item.PublicPort)))
            throw new InvalidOperationException("A requested host port is already published by another container.");

        if (configuration.Networks.Length == 0) return;
        var networks = await ListNetworksAsync(cancellationToken);
        var available = networks.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var network in configuration.Networks)
        {
            if (!available.Contains(network)) throw new InvalidOperationException($"Docker network was not found: {network}");
            if (network.StartsWith("database-platform", StringComparison.OrdinalIgnoreCase) ||
                network.StartsWith("whaledeck", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Protected platform networks cannot be joined from the generic container workflow.");
        }
    }

    private async Task<ContainerListResponse> ResolveContainerAsync(string value, CancellationToken cancellationToken)
    {
        var containers = await ListContainersAsync(true, cancellationToken);
        var matches = containers.Where(item => item.ID.StartsWith(value, StringComparison.OrdinalIgnoreCase) ||
            item.Names.Any(name => string.Equals(name.TrimStart('/'), value.TrimStart('/'), StringComparison.Ordinal))).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException("Container was not found."),
            _ => throw new InvalidOperationException("Container id is ambiguous.")
        };
    }

    private static ContainerConfiguration ParseContainerConfiguration(IReadOnlyDictionary<string, string> parameters)
    {
        var image = Required(parameters, "image");
        var name = Required(parameters, "name");
        ValidateImage(image);
        if (!ContainerNamePattern.IsMatch(name)) throw new InvalidOperationException("Container name is invalid.");
        if (name.StartsWith("database-platform-", StringComparison.OrdinalIgnoreCase) || name.StartsWith("whaledeck-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Reserved container name prefix.");
        var restartPolicy = parameters.GetValueOrDefault("restartPolicy", "unless-stopped") switch
        {
            "no" => RestartPolicyKind.No,
            "unless-stopped" => RestartPolicyKind.UnlessStopped,
            "on-failure" => RestartPolicyKind.OnFailure,
            _ => throw new InvalidOperationException("Container restart policy is unsupported.")
        };
        var healthCommand = parameters.GetValueOrDefault("healthCommand")?.Trim();
        HealthConfig? healthcheck = null;
        if (!string.IsNullOrWhiteSpace(healthCommand))
        {
            if (healthCommand.Length > 1024 || healthCommand.Contains('\0') || healthCommand.Contains('\n'))
                throw new InvalidOperationException("Container health command is invalid.");
            healthcheck = new HealthConfig
            {
                Test = ["CMD-SHELL", healthCommand],
                Interval = TimeSpan.FromSeconds(ParseLong(parameters, "healthIntervalSeconds", 30, 5, 3600)),
                Timeout = TimeSpan.FromSeconds(ParseLong(parameters, "healthTimeoutSeconds", 5, 1, 300)),
                Retries = ParseLong(parameters, "healthRetries", 3, 1, 20)
            };
        }
        return new ContainerConfiguration(
            name,
            image,
            ParseLong(parameters, "memoryMb", 512, 32, 32768),
            ParseDouble(parameters, "cpus", 1, 0.1, 32),
            parameters.TryGetValue("autoUpdate", out var configured) && bool.TryParse(configured, out var enabled) && enabled,
            restartPolicy,
            ParseEnvironment(parameters.GetValueOrDefault("environment")),
            ParseStringArray(parameters.GetValueOrDefault("command"), "command"),
            ParseStringArray(parameters.GetValueOrDefault("entrypoint"), "entrypoint"),
            ParsePortBindings(parameters.GetValueOrDefault("ports")),
            ParseMounts(parameters.GetValueOrDefault("volumes")),
            ParseNetworks(parameters.GetValueOrDefault("networks")),
            ParseLabels(parameters.GetValueOrDefault("labels")),
            healthcheck);
    }

    private static List<string> ParseEnvironment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        if (value.Length > 64 * 1024 || value.Contains('\0')) throw new InvalidOperationException("Container environment is too large or invalid.");
        var result = new List<string>();
        foreach (var line in value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            if (separator < 1 || !EnvironmentNamePattern.IsMatch(line[..separator]))
                throw new InvalidOperationException("A container environment variable name is invalid.");
            result.Add(line);
        }
        return result;
    }

    private static string[]? ParseStringArray(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            var values = JsonSerializer.Deserialize<string[]>(value);
            if (values is null || values.Length > 64 || values.Any(item => string.IsNullOrEmpty(item) || item.Length > 2048 || item.Contains('\0')))
                throw new InvalidOperationException($"Container {name} is invalid.");
            return values;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Container {name} must be a JSON string array.", exception);
        }
    }

    private static Dictionary<string, IList<PortBinding>> ParsePortBindings(string? value)
    {
        var result = new Dictionary<string, IList<PortBinding>>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value)) return result;
        foreach (var mapping in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!PortPattern.IsMatch(mapping)) throw new InvalidOperationException("A container port mapping is invalid.");
            var protocol = mapping.EndsWith("/udp", StringComparison.OrdinalIgnoreCase) ? "udp" : "tcp";
            var withoutProtocol = mapping.EndsWith("/tcp", StringComparison.OrdinalIgnoreCase) || mapping.EndsWith("/udp", StringComparison.OrdinalIgnoreCase)
                ? mapping[..^4]
                : mapping;
            var parts = withoutProtocol.Split(':');
            if (parts.Length != 3 || !ushort.TryParse(parts[1], out var hostPort) || !ushort.TryParse(parts[2], out var containerPort) ||
                hostPort == 0 || containerPort == 0)
                throw new InvalidOperationException("A container port mapping is outside the valid range.");
            var key = $"{containerPort}/{protocol}";
            if (!result.TryAdd(key, [new PortBinding { HostIP = parts[0], HostPort = hostPort.ToString(System.Globalization.CultureInfo.InvariantCulture) }]))
                throw new InvalidOperationException("A container port is mapped more than once.");
        }
        return result;
    }

    private static List<Mount> ParseMounts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var result = new List<Mount>();
        foreach (var mapping in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = mapping.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length is < 2 or > 3 || !VolumeNamePattern.IsMatch(parts[0]) || !parts[1].StartsWith('/') ||
                parts[1].Contains("..", StringComparison.Ordinal) || parts[1].Contains('\0') ||
                parts[0].StartsWith("database-platform", StringComparison.OrdinalIgnoreCase) || parts[0].StartsWith("whaledeck", StringComparison.OrdinalIgnoreCase) ||
                parts.Length == 3 && parts[2] is not ("ro" or "rw"))
                throw new InvalidOperationException("A container volume mapping is invalid.");
            result.Add(new Mount { Type = "volume", Source = parts[0], Target = parts[1], ReadOnly = parts.Length == 3 && parts[2] == "ro" });
        }
        return result;
    }

    private static string[] ParseNetworks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(item => NetworkNamePattern.IsMatch(item) ? item : throw new InvalidOperationException("A Docker network name is invalid."))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, string> ParseLabels(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var labels = JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new Dictionary<string, string>();
            if (labels.Count > 32 || labels.Any(item => !LabelNamePattern.IsMatch(item.Key) || item.Value.Length > 1024 ||
                item.Value.Any(char.IsControl) || item.Key.StartsWith("io.whaledeck.", StringComparison.OrdinalIgnoreCase) ||
                item.Key.StartsWith("com.docker.compose.", StringComparison.OrdinalIgnoreCase) ||
                IsSensitiveLabelName(item.Key)))
                throw new InvalidOperationException("Container labels contain an invalid or reserved entry.");
            return new Dictionary<string, string>(labels, StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Container labels must be a JSON object of string values.", exception);
        }
    }

    private static bool IsSensitiveLabelName(string key) =>
        key.Contains("password", StringComparison.OrdinalIgnoreCase) || key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("token", StringComparison.OrdinalIgnoreCase) || key.Contains("credential", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> NormalizePlanParameters(IReadOnlyDictionary<string, string> parameters)
    {
        var normalized = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        normalized.Remove("password");
        normalized.Remove("environment");
        normalized.Remove("inputTicket");
        return normalized;
    }

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

    private sealed record ContainerConfiguration(
        string Name,
        string Image,
        long MemoryMb,
        double Cpus,
        bool AutoUpdate,
        RestartPolicyKind RestartPolicy,
        List<string> Environment,
        string[]? Command,
        string[]? Entrypoint,
        Dictionary<string, IList<PortBinding>> Ports,
        List<Mount> Mounts,
        string[] Networks,
        Dictionary<string, string> Labels,
        HealthConfig? Healthcheck);
}
