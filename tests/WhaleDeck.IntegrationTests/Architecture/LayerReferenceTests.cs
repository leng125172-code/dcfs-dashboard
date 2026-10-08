namespace WhaleDeck.IntegrationTests.Architecture;

public sealed class LayerReferenceTests
{
    [Fact]
    public void ApiAssemblyLoadsSuccessfully()
    {
        var assembly = typeof(Program).Assembly;

        Assert.Equal("WhaleDeck.Api", assembly.GetName().Name);
    }
}
