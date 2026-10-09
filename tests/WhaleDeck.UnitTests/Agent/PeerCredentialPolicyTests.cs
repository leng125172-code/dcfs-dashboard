using Microsoft.Extensions.Configuration;
using WhaleDeck.Agent.Services;

namespace WhaleDeck.UnitTests.Agent;

public sealed class PeerCredentialPolicyTests
{
    [Theory]
    [InlineData(0u, 1u, true)]
    [InlineData(1000u, 975u, true)]
    [InlineData(1000u, 1000u, false)]
    public void AllowsOnlyRootOrConfiguredPrimaryGroup(uint userId, uint groupId, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agent:AllowedPeerGid"] = "975"
        }).Build();
        var policy = new PeerCredentialPolicy(configuration);

        Assert.Equal(expected, policy.IsAllowed(userId, groupId));
    }
}
