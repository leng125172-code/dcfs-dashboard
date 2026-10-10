using System.Reflection;
using WhaleDeck.Agent.Services;

namespace WhaleDeck.UnitTests.Agent;

public sealed class ManagedActionExecutorTests
{
    [Theory]
    [InlineData("alpine:3.22")]
    [InlineData("library/alpine:3.22")]
    [InlineData("ghcr.io/example/service:1.2.3")]
    public void ImageValidationAcceptsTagsAndApprovedRegistries(string image) => InvokeValidateImage(image);

    [Theory]
    [InlineData("unapproved.example/service:1.0")]
    [InlineData("localhost/service:1.0")]
    [InlineData("../service:1.0")]
    public void ImageValidationRejectsUnapprovedOrUnsafeReferences(string image)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeValidateImage(image));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Theory]
    [InlineData("chore: save workstation configuration")]
    [InlineData("文档：保存工作站配置")]
    public void CommitMessageValidationAcceptsBoundedText(string message) => InvokePrivate("ValidateCommitMessage", message);

    [Theory]
    [InlineData("no")]
    [InlineData("line one\nline two")]
    [InlineData("contains\0control")]
    public void CommitMessageValidationRejectsShortOrControlText(string message)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokePrivate("ValidateCommitMessage", message));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Theory]
    [InlineData(".env", true)]
    [InlineData("authentik/.env.production", true)]
    [InlineData("certificates/server.key", true)]
    [InlineData("settings/client-secret.json", true)]
    [InlineData(".env.example", false)]
    [InlineData("validation/check-whaledeck-credentials.sh", false)]
    public void RepositoryFileValidationDistinguishesSecretsFromTemplatesAndTools(string path, bool expected)
    {
        var method = typeof(ManagedActionExecutor).GetMethod("IsSensitiveRepositoryFile", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsSensitiveRepositoryFile was not found.");
        Assert.Equal(expected, method.Invoke(null, [path]));
    }

    [Theory]
    [InlineData("compose.yml", true)]
    [InlineData("settings.json", true)]
    [InlineData("service.toml", true)]
    [InlineData("scripts/backup.sh", false)]
    [InlineData("docs/README.md", false)]
    public void RepositoryContentInspectionTargetsConfigurationFormats(string path, bool expected)
    {
        var method = typeof(ManagedActionExecutor).GetMethod("ShouldInspectRepositoryContent", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldInspectRepositoryContent was not found.");
        Assert.Equal(expected, method.Invoke(null, [path]));
    }

    private static void InvokeValidateImage(string image)
        => InvokePrivate("ValidateImage", image);

    private static void InvokePrivate(string methodName, string value)
    {
        var method = typeof(ManagedActionExecutor).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{methodName} was not found.");
        method.Invoke(null, [value]);
    }
}
