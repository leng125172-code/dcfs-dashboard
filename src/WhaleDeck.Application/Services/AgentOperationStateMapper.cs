using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public static class AgentOperationStateMapper
{
    public static JobStateTransition Map(AgentOperationDto operation)
    {
        var state = operation.State switch
        {
            "Succeeded" => "Succeeded",
            "Failed" => "Failed",
            "Canceled" => "Canceled",
            "RolledBack" => "Failed",
            "RollingBack" => "Running",
            "Running" => "Running",
            "WaitingForHealth" => "Waiting",
            "Queued" => "Waiting",
            _ => "Waiting"
        };

        return new JobStateTransition(
            state,
            operation.Phase,
            checked((short)Math.Clamp(operation.ProgressPercent, 0, 100)),
            operation.ErrorCode);
    }
}

public sealed record JobStateTransition(string State, string Phase, short ProgressPercent, string? ErrorCode);
