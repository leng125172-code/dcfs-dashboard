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

    [Theory]
    [InlineData("*")]
    [InlineData("1.*")]
    [InlineData("1.2.*")]
    [InlineData(">=1.2.3")]
    [InlineData("v2.4.1")]
    public void VersionPolicyAcceptsSupportedSyntax(string policy) => Invoke("ValidateVersionPolicy", policy);

    [Theory]
    [InlineData("latest")]
    [InlineData("1.x")]
    [InlineData(">=")]
    public void VersionPolicyRejectsAmbiguousSyntax(string policy) => AssertInvalid("ValidateVersionPolicy", policy);

    [Theory]
    [InlineData("1.2.4", "1.2.*", true)]
    [InlineData("1.3.0", "1.2.*", false)]
    [InlineData("2.0.0", ">=1.2.3", true)]
    public void VersionMatchingHonorsPolicy(string version, string policy, bool expected) =>
        Assert.Equal(expected, InvokeBoolean("VersionMatchesPolicy", version, policy));

    [Fact]
    public void ShanghaiMaintenanceWindowUsesConfiguredDayAndTime()
    {
        var inside = new DateTimeOffset(2026, 10, 11, 12, 30, 0, TimeSpan.Zero);
        var outside = inside.AddHours(4);
        Assert.True(InvokeBoolean("IsInMaintenanceWindow", "Sun@20:00-21:00", inside));
        Assert.False(InvokeBoolean("IsInMaintenanceWindow", "Sun@20:00-21:00", outside));
    }

    [Theory]
    [InlineData("team-apps")]
    [InlineData("project_01")]
    public void DockerResourceNamesAcceptNormalNames(string name) =>
        Assert.Equal(name, Invoke("ValidateDockerResourceName", name, "network"));

    [Theory]
    [InlineData("database-platform-internal")]
    [InlineData("whaledeck-core")]
    [InlineData("../escape")]
    public void DockerResourceNamesRejectProtectedOrInvalidNames(string name)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => Invoke("ValidateDockerResourceName", name, "network"));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
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

    private static bool InvokeBoolean(string methodName, params object[] values)
    {
        var method = typeof(DockerEngine).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        return Assert.IsType<bool>(method.Invoke(null, values));
    }


    private static object? Invoke(string methodName, params object[] values)
    {
        var method = typeof(DockerEngine).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        return method.Invoke(null, values);
    }
}
