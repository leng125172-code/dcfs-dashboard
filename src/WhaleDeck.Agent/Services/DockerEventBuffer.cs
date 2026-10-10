using System.Security.Cryptography;
using System.Text;
using Docker.DotNet.Models;

namespace WhaleDeck.Agent.Services;

public sealed record BufferedDockerEvent(
    string Fingerprint,
    DateTimeOffset OccurredAtUtc,
    string EventType,
    string Action,
    string ResourceId,
    string ResourceName,
    string Image,
    IReadOnlyDictionary<string, string> Attributes);

public sealed class DockerEventBuffer
{
    private const int Capacity = 2_048;
    private readonly object _gate = new();
    private readonly Queue<BufferedDockerEvent> _events = new();
    private readonly HashSet<string> _fingerprints = new(StringComparer.Ordinal);

    public void Add(Message message)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in (message.Actor?.Attributes ?? new Dictionary<string, string>())
                     .Where(item => !Sensitive(item.Key ?? string.Empty))
                     .OrderBy(item => item.Key, StringComparer.Ordinal)
                     .Take(64))
        {
            var key = Limit(attribute.Key ?? string.Empty, 128);
            if (key.Length > 0) attributes[key] = Limit(attribute.Value ?? string.Empty, 256);
        }
        var resourceId = Limit(message.Actor?.ID ?? message.ID ?? string.Empty, 128);
        var eventType = Limit(message.Type ?? "unknown", 32);
        var action = Limit(message.Action ?? message.Status ?? "unknown", 64);
        var occurredAt = OccurredAt(message);
        var material = $"{message.TimeNano}|{message.Time}|{eventType}|{action}|{resourceId}|" +
            string.Join('|', attributes.Select(item => $"{item.Key}={item.Value}"));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        var item = new BufferedDockerEvent(
            fingerprint,
            occurredAt,
            eventType,
            action,
            resourceId,
            attributes.GetValueOrDefault("name", resourceId),
            attributes.GetValueOrDefault("image", Limit(message.From ?? string.Empty, 256)),
            attributes);

        lock (_gate)
        {
            if (!_fingerprints.Add(fingerprint)) return;
            _events.Enqueue(item);
            while (_events.Count > Capacity)
            {
                _fingerprints.Remove(_events.Dequeue().Fingerprint);
            }
        }
    }

    public IReadOnlyCollection<BufferedDockerEvent> Snapshot(DateTimeOffset sinceUtc, int take)
    {
        lock (_gate)
        {
            return _events.Where(item => item.OccurredAtUtc >= sinceUtc)
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(Math.Clamp(take, 1, Capacity))
                .ToArray();
        }
    }

    private static DateTimeOffset OccurredAt(Message message)
    {
        if (message.TimeNano > 0) return DateTimeOffset.FromUnixTimeMilliseconds(message.TimeNano / 1_000_000);
        return message.Time > 0 ? DateTimeOffset.FromUnixTimeSeconds(message.Time) : DateTimeOffset.UtcNow;
    }

    private static bool Sensitive(string key) =>
        key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith("env", StringComparison.OrdinalIgnoreCase);

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];
}
