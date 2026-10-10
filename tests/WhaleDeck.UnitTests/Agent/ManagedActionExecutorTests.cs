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

    private static void InvokeValidateImage(string image)
    {
        var method = typeof(ManagedActionExecutor).GetMethod("ValidateImage", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ValidateImage was not found.");
        method.Invoke(null, [image]);
    }
}
