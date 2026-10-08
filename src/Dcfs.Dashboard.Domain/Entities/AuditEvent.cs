namespace Dcfs.Dashboard.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string ActorId { get; init; }

    public required string Action { get; init; }

    public required string Target { get; init; }

    public string? DetailJson { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
