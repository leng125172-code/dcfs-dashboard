using System.Collections.Concurrent;
using System.Text.Json;
using WhaleDeck.Contracts.Agent.V1;

namespace WhaleDeck.Agent.Services;

public sealed class OperationStore
{
    private readonly ConcurrentDictionary<string, OperationHandle> _operations = new(StringComparer.Ordinal);
    private readonly string _directory;
    private readonly string _maintenancePath;

    public OperationStore(IConfiguration configuration)
    {
        _directory = configuration["Agent:OperationDirectory"] ?? "/var/lib/whaledeck-agent/operations";
        _maintenancePath = configuration["Agent:MaintenanceStatusPath"] ?? "/run/whaledeck/maintenance/status.json";
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(Path.GetDirectoryName(_maintenancePath)!);
        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var operation = JsonSerializer.Deserialize<OperationHandle>(File.ReadAllText(file));
                if (operation is not null)
                {
                    _operations[operation.OperationId] = operation;
                }
            }
            catch (JsonException)
            {
                // A corrupt operation is ignored here and remains available for diagnostics.
            }
        }
    }

    public OperationHandle Create(string phase)
    {
        var operation = new OperationHandle
        {
            OperationId = Guid.NewGuid().ToString("D"),
            State = OperationState.Queued,
            Phase = phase,
            ProgressPercent = 0
        };
        Save(operation);
        return operation;
    }

    public OperationHandle? Get(string id) => _operations.GetValueOrDefault(id);

    public OperationHandle GetMaintenance()
    {
        if (!File.Exists(_maintenancePath))
        {
            return new OperationHandle { State = OperationState.Succeeded, Phase = "Ready", ProgressPercent = 100 };
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_maintenancePath));
            var root = document.RootElement;
            return new OperationHandle
            {
                OperationId = root.TryGetProperty("operationId", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Phase = root.TryGetProperty("phase", out var phase) ? phase.GetString() ?? "Unknown" : "Unknown",
                State = root.TryGetProperty("state", out var state) && Enum.TryParse<OperationState>(state.GetString(), true, out var parsed)
                    ? parsed
                    : OperationState.Running
            };
        }
        catch (JsonException)
        {
            return new OperationHandle { State = OperationState.Failed, Phase = "InvalidMaintenanceState", ErrorCode = "AGENT_STATE_INVALID" };
        }
    }

    public void Save(OperationHandle operation, string? maintenanceMessage = null)
    {
        _operations[operation.OperationId] = operation;
        AtomicWrite(Path.Combine(_directory, $"{operation.OperationId}.json"), JsonSerializer.Serialize(operation));
        if (maintenanceMessage is not null)
        {
            AtomicWrite(_maintenancePath, JsonSerializer.Serialize(new
            {
                operationId = operation.OperationId,
                state = operation.State.ToString(),
                phase = operation.Phase,
                message = maintenanceMessage,
                observedAtUtc = DateTimeOffset.UtcNow
            }));
        }
    }

    private static void AtomicWrite(string path, string content)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, true);
    }
}
