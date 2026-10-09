using WhaleDeck.Application.Models;
using WhaleDeck.Application.Services;

namespace WhaleDeck.UnitTests.Application;

public sealed class AgentOperationStateMapperTests
{
    [Theory]
    [InlineData("Queued", "Waiting")]
    [InlineData("WaitingForHealth", "Waiting")]
    [InlineData("Running", "Running")]
    [InlineData("RollingBack", "Running")]
    [InlineData("Succeeded", "Succeeded")]
    [InlineData("Failed", "Failed")]
    [InlineData("RolledBack", "Failed")]
    [InlineData("Canceled", "Canceled")]
    [InlineData("FutureMinorVersionState", "Waiting")]
    public void MapsAgentStateToStableJobState(string agentState, string expectedJobState)
    {
        var transition = AgentOperationStateMapper.Map(new AgentOperationDto("operation", agentState, "phase", 42, null));

        Assert.Equal(expectedJobState, transition.State);
        Assert.Equal((short)42, transition.ProgressPercent);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(101, 100)]
    public void ClampsUntrustedProgress(int progress, short expected)
    {
        var transition = AgentOperationStateMapper.Map(new AgentOperationDto("operation", "Running", "phase", progress, null));

        Assert.Equal(expected, transition.ProgressPercent);
    }
}
