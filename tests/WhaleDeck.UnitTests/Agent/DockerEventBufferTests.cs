using Docker.DotNet.Models;
using WhaleDeck.Agent.Services;

namespace WhaleDeck.UnitTests.Agent;

public sealed class DockerEventBufferTests
{
    [Fact]
    public void BufferDeduplicatesAndRedactsSensitiveAttributes()
    {
        var buffer = new DockerEventBuffer();
        var message = new Message
        {
            Type = "container",
            Action = "start",
            Time = 1_700_000_000,
            TimeNano = 1_700_000_000_123_000_000,
            Actor = new Actor
            {
                ID = "container-id",
                Attributes = new Dictionary<string, string>
                {
                    ["name"] = "demo",
                    ["image"] = "nginx:alpine",
                    ["password"] = "must-not-leak",
                    ["env"] = "TOKEN=must-not-leak"
                }
            }
        };

        buffer.Add(message);
        buffer.Add(message);

        var item = Assert.Single(buffer.Snapshot(DateTimeOffset.FromUnixTimeSeconds(1_600_000_000), 20));
        Assert.Equal("demo", item.ResourceName);
        Assert.Equal("nginx:alpine", item.Image);
        Assert.DoesNotContain("password", item.Attributes.Keys);
        Assert.DoesNotContain("env", item.Attributes.Keys);
    }

    [Fact]
    public void BufferKeepsOnlyBoundedRecentEvents()
    {
        var buffer = new DockerEventBuffer();
        for (var index = 0; index < 2_100; index++)
        {
            buffer.Add(new Message
            {
                Type = "container",
                Action = "update",
                Time = 1_700_000_000 + index,
                Actor = new Actor { ID = $"container-{index}" }
            });
        }

        Assert.Equal(2_048, buffer.Snapshot(DateTimeOffset.UnixEpoch, 3_000).Count);
    }
}
