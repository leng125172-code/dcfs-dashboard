namespace WhaleDeck.Application.Models;

public sealed record PortalItemDto(
    Guid Id,
    string Scope,
    string Name,
    string? Description,
    string Url,
    string IconKind,
    string IconValue,
    string Color,
    int SortOrder,
    bool IsEnabled,
    long Version);

public sealed record SavePortalItemCommand(
    Guid? Id,
    string Scope,
    string Name,
    string? Description,
    string Url,
    string IconKind,
    string IconValue,
    string Color,
    int SortOrder,
    bool IsEnabled,
    long? ExpectedVersion);

public sealed record CurrentUserDto(
    string Subject,
    string? Name,
    IReadOnlyCollection<string> Groups,
    bool IsAdministrator,
    DateTimeOffset PermissionExpiresAtUtc);

public sealed record AgentHealthDto(
    bool Available,
    bool DockerAvailable,
    bool SystemdAvailable,
    string Status,
    DateTimeOffset ObservedAtUtc);

public sealed record HostInfoDto(
    string HostName,
    string Distribution,
    string KernelVersion,
    string Architecture,
    DateTimeOffset BootTimeUtc,
    TimeSpan Uptime,
    int LogicalProcessorCount,
    ulong TotalMemoryBytes);

public sealed record ManagedResourceDto(
    string Id,
    string Name,
    string Type,
    string State,
    string Version,
    bool IsProtected,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record ContainerDto(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    bool IsProtected,
    string ProtectionLevel,
    IReadOnlyDictionary<string, string> Labels);

public sealed record OverviewDto(
    string Scope,
    string PlatformStatus,
    DateTimeOffset SampledAtUtc,
    IReadOnlyCollection<PortalItemDto> Portals,
    AgentHealthDto? Agent,
    HostInfoDto? Host,
    IReadOnlyCollection<ManagedResourceDto>? Resources,
    IReadOnlyCollection<MetricValueDto>? Metrics,
    IReadOnlyCollection<CatalogApplicationDto>? Applications,
    bool CatalogIsStale);

public sealed record OperationCommand(
    string Area,
    string Action,
    string? ResourceId,
    string IdempotencyKey,
    string RequestJson,
    string? PlanHash,
    bool Confirmed);

public sealed record JobDto(
    Guid Id,
    string JobType,
    string State,
    string Phase,
    short? ProgressPercent,
    string? ErrorCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record JobEventDto(
    long Sequence,
    string State,
    string Phase,
    short? ProgressPercent,
    string MessageCode,
    DateTimeOffset OccurredAtUtc);

public sealed record PlanDto(
    string PlanHash,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<string> Changes,
    IReadOnlyCollection<string> Warnings);

public sealed record MetricValueDto(string Kind, string DeviceId, double Value, string Unit, string Quality, DateTimeOffset SampledAtUtc);

public sealed record MetricPointDto(DateTimeOffset SampledAtUtc, double? Value, string Quality);
public sealed record MetricSeriesSnapshotDto(string Kind, string DeviceId, string Unit, IReadOnlyCollection<MetricPointDto> Points);

public sealed record AgentOperationDto(string OperationId, string State, string Phase, int ProgressPercent, string? ErrorCode);

public sealed record OneTimeSecretTicketDto(string Token, DateTimeOffset ExpiresAtUtc);

public sealed record CatalogApplicationDto(
    string Id,
    string Name,
    string Description,
    string Image,
    string Version,
    string? IconUrl,
    bool Installed,
    string? State);

public sealed record CatalogResultDto(
    IReadOnlyCollection<CatalogApplicationDto> Applications,
    DateTimeOffset FetchedAtUtc,
    bool IsStale);
