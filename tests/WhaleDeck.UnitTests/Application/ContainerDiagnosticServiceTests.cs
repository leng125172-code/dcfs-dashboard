using System.Reflection;
using WhaleDeck.Application.Services;

namespace WhaleDeck.UnitTests.Application;

public sealed class ContainerDiagnosticServiceTests
{
    [Theory]
    [InlineData("password=hunter2", "password=[REDACTED]")]
    [InlineData("token: abc.def", "token: [REDACTED]")]
    [InlineData("Authorization=Bearer-value", "Authorization=[REDACTED]")]
    [InlineData("header Bearer abc.def.ghi", "header Bearer [REDACTED]")]
    public void DiagnosticLogsRedactRecognizableSecrets(string input, string expected)
    {
        var method = typeof(ContainerDiagnosticService).GetMethod(
            "RedactLogLine",
            BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Redactor was not found.");
        Assert.Equal(expected, method.Invoke(null, [input]));
    }
}
