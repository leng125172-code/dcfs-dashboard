using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WhaleDeck.Application.Abstractions;

namespace WhaleDeck.Application.Services;

public sealed partial class ContainerDiagnosticService(IAgentGateway agent)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] IncludedFiles = ["inspect.json", "stats.json", "events.json", "logs.txt"];

    public async Task<ContainerDiagnosticBundle> CreateAsync(string containerId, CancellationToken cancellationToken)
    {
        var inspect = await agent.InspectContainerAsync(containerId, cancellationToken);
        var logsTask = agent.GetContainerLogsAsync(containerId, 500, 60, cancellationToken);
        var statsTask = agent.GetContainerStatsAsync(containerId, cancellationToken);
        var eventsTask = agent.ListDockerEventsAsync(DateTimeOffset.UtcNow.AddDays(-1), 500, cancellationToken);
        await Task.WhenAll(logsTask, statsTask, eventsTask);

        var events = (await eventsTask).Where(item =>
            string.Equals(item.ResourceId, inspect.Id, StringComparison.Ordinal) ||
            string.Equals(item.ResourceName, inspect.Name, StringComparison.Ordinal)).Take(200).ToArray();
        var logs = (await logsTask).Lines.Select(RedactLogLine).ToArray();

        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteJsonAsync(archive, "manifest.json", new
            {
                schemaVersion = 1,
                generatedAtUtc = DateTimeOffset.UtcNow,
                containerId = inspect.Id,
                containerName = inspect.Name,
                includes = IncludedFiles,
                notes = "Environment values and sensitive labels are excluded. Recognizable secrets in logs are redacted."
            }, cancellationToken);
            await WriteJsonAsync(archive, "inspect.json", inspect, cancellationToken);
            await WriteJsonAsync(archive, "stats.json", await statsTask, cancellationToken);
            await WriteJsonAsync(archive, "events.json", events, cancellationToken);
            var logEntry = archive.CreateEntry("logs.txt", CompressionLevel.Fastest);
            await using var logStream = logEntry.Open();
            await using var writer = new StreamWriter(logStream, new UTF8Encoding(false));
            foreach (var line in logs) await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        return new ContainerDiagnosticBundle(
            output.ToArray(),
            $"whaledeck-container-{SafeFileSegment(inspect.Name)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.zip");
    }

    private static async Task WriteJsonAsync(
        ZipArchive archive,
        string name,
        object value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, value.GetType(), JsonOptions, cancellationToken);
    }

    internal static string RedactLogLine(string line)
    {
        var redacted = SecretAssignmentPattern().Replace(line, "$1$2[REDACTED]");
        return BearerPattern().Replace(redacted, "$1[REDACTED]");
    }

    private static string SafeFileSegment(string value)
    {
        var safe = string.Concat(value.Take(64).Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-'));
        return string.IsNullOrWhiteSpace(safe) ? "container" : safe;
    }

    [GeneratedRegex("(?i)\\b(password|passwd|token|secret|api[_-]?key|credential|authorization)(\\s*[:=]\\s*)([^\\s,;]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex("(?i)\\b(Bearer\\s+)[A-Za-z0-9._~+\\-/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();
}

public sealed record ContainerDiagnosticBundle(byte[] Content, string FileName);
