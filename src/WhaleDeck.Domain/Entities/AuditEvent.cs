namespace WhaleDeck.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string ActorSubject { get; init; }

    public required string Action { get; init; }

    public required string TargetType { get; init; }

    public required string TargetId { get; init; }

    public string Result { get; init; } = "Succeeded";

    public string? SourceIp { get; init; }

    public Guid? JobId { get; init; }

    public required string TraceId { get; init; }

    public string? DetailJson { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
