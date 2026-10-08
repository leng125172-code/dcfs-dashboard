namespace Dcfs.Dashboard.IntegrationTests.Architecture;

public sealed class LayerReferenceTests
{
    [Fact]
    public void ApiAssemblyLoadsSuccessfully()
    {
        var assembly = typeof(Program).Assembly;

        Assert.Equal("Dcfs.Dashboard.Api", assembly.GetName().Name);
    }
}
