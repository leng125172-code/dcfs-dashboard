using WhaleDeck.Infrastructure.Secrets;

namespace WhaleDeck.IntegrationTests.Secrets;

public sealed class MemoryOneTimeSecretStoreTests
{
    [Fact]
    public async Task SecretCanOnlyBeConsumedOnceByItsOwner()
    {
        var store = new MemoryOneTimeSecretStore();
        var ticket = await store.StoreAsync("owner-a", "top-secret", CancellationToken.None);

        Assert.Null(await store.ConsumeAsync("owner-b", ticket.Token, CancellationToken.None));
        Assert.Equal("top-secret", await store.ConsumeAsync("owner-a", ticket.Token, CancellationToken.None));
        Assert.Null(await store.ConsumeAsync("owner-a", ticket.Token, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("../../etc/passwd")]
    public async Task InvalidTokensAreRejected(string token)
    {
        var store = new MemoryOneTimeSecretStore();

        Assert.Null(await store.ConsumeAsync("owner", token, CancellationToken.None));
    }
}
