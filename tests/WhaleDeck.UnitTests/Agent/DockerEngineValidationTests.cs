using System.Reflection;
using WhaleDeck.Agent.Services;

namespace WhaleDeck.UnitTests.Agent;

public sealed class DockerEngineValidationTests
{
    [Theory]
    [InlineData("0.0.0.0:8080:80/tcp")]
    [InlineData("127.0.0.1:5353:53/udp")]
    public void PortMappingsAcceptApprovedBindings(string mapping) => Invoke("ParsePortBindings", mapping);

    [Theory]
    [InlineData("192.168.1.1:8080:80/tcp")]
    [InlineData("0.0.0.0:99999:80/tcp")]
    [InlineData("0.0.0.0:8080:0/tcp")]
    public void PortMappingsRejectUnsafeOrOutOfRangeBindings(string mapping) => AssertInvalid("ParsePortBindings", mapping);

    [Theory]
    [InlineData("app-data:/var/lib/app:rw")]
    [InlineData("app-config:/etc/app:ro")]
    public void VolumeMappingsAcceptNamedVolumes(string mapping) => Invoke("ParseMounts", mapping);

    [Theory]
    [InlineData("/data/private:/var/lib/app:rw")]
    [InlineData("database-platform-postgres:/var/lib/postgresql:rw")]
    [InlineData("whaledeck-core:/data:rw")]
    public void VolumeMappingsRejectHostPathsAndProtectedVolumes(string mapping) => AssertInvalid("ParseMounts", mapping);

    [Theory]
    [InlineData("{\"team\":\"platform\"}")]
    [InlineData("{\"com.example.role\":\"api\"}")]
    public void LabelsAcceptNonSensitiveMetadata(string json) => Invoke("ParseLabels", json);

    [Theory]
    [InlineData("{\"io.whaledeck.protected\":\"false\"}")]
    [InlineData("{\"com.docker.compose.project\":\"forged\"}")]
    [InlineData("{\"api-token\":\"secret-value\"}")]
    public void LabelsRejectReservedOrSensitiveKeys(string json) => AssertInvalid("ParseLabels", json);

    [Fact]
    public void PlanParametersNeverContainSecretValuesOrTickets()
    {
        var parameters = new Dictionary<string, string>
        {
            ["name"] = "demo",
            ["image"] = "nginx:latest",
            ["environment"] = "PASSWORD=hidden",
            ["password"] = "hidden",
            ["inputTicket"] = "ticket",
            ["inputKind"] = "environment"
        };

        var normalized = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
            Invoke("NormalizePlanParameters", parameters));

        Assert.DoesNotContain("environment", normalized.Keys);
        Assert.DoesNotContain("password", normalized.Keys);
        Assert.DoesNotContain("inputTicket", normalized.Keys);
        Assert.Equal("environment", normalized["inputKind"]);
        Assert.Equal("demo", normalized["name"]);
    }

    private static void AssertInvalid(string method, string value)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => Invoke(method, value));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static object? Invoke(string methodName, string value)
    {
        var method = typeof(DockerEngine).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        return method.Invoke(null, [value]);
    }

    private static object? Invoke(string methodName, IReadOnlyDictionary<string, string> value)
    {
        var method = typeof(DockerEngine).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        return method.Invoke(null, [value]);
    }
}
