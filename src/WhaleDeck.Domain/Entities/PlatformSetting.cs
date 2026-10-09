namespace WhaleDeck.Domain.Entities;

public sealed class PlatformSetting
{
    public required string Key { get; init; }

    public required string ValueJson { get; set; }

    public long Version { get; set; }

    public required string UpdatedBySubject { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
