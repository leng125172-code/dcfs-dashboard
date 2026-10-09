using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WhaleDeck.Infrastructure.Identity;

namespace WhaleDeck.IntegrationTests.Security;

public sealed class PermissionSnapshotTests
{
    private static readonly string[] AdministratorGroups = ["admin-id"];
    [Fact]
    public async Task MissingApiTokenDoesNotPromoteCookieClaims()
    {
        var directory = new AuthentikIdentityDirectory(new StubClientFactory(HttpStatusCode.ServiceUnavailable), NewCache(), Config());
        var current = await directory.ResolveCurrentAsync("42", "User", ["admin-id"], default);
        Assert.False(current.IsAdministrator);
    }

    [Fact]
    public async Task ExpiredSnapshotCannotPreserveAdministratorWhenAuthentikFails()
    {
        var cache = NewCache();
        await cache.SetStringAsync("whaledeck:authz:42", JsonSerializer.Serialize(new
        {
            Groups = AdministratorGroups, Administrator = true, ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        }));
        var directory = new AuthentikIdentityDirectory(new StubClientFactory(HttpStatusCode.ServiceUnavailable), cache, Config("test-token"));
        var current = await directory.ResolveCurrentAsync("42", "User", ["admin-id"], default);
        Assert.False(current.IsAdministrator);
        Assert.Empty(current.Groups);
    }

    [Fact]
    public async Task ValidSnapshotSurvivesShortAuthentikOutage()
    {
        var cache = NewCache();
        var expires = DateTimeOffset.UtcNow.AddMinutes(30);
        await cache.SetStringAsync("whaledeck:authz:42", JsonSerializer.Serialize(new
        {
            Groups = AdministratorGroups, Administrator = true, ExpiresAtUtc = expires
        }));
        var directory = new AuthentikIdentityDirectory(new StubClientFactory(HttpStatusCode.ServiceUnavailable), cache, Config("test-token"));
        var current = await directory.ResolveCurrentAsync("42", "User", [], default);
        Assert.True(current.IsAdministrator);
        Assert.Equal(expires, current.PermissionExpiresAtUtc);
    }

    [Fact]
    public async Task ExplicitlyDisabledAccountIsRejected()
    {
        var factory = new StubClientFactory(HttpStatusCode.OK, """{"pk":42,"is_active":false,"groups_obj":[{"pk":"admin-id"}]}""");
        var directory = new AuthentikIdentityDirectory(factory, NewCache(), Config("test-token"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => directory.ResolveCurrentAsync("42", "User", ["admin-id"], default));
    }

    private static MemoryDistributedCache NewCache() => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static IConfigurationRoot Config(string? token = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Authentication:Authentik:ApiToken"] = token,
        ["Authentication:Authentik:AdministratorGroupId"] = "admin-id",
        ["Authentication:Authentik:AllowClaimFallback"] = "false"
    }).Build();

    private sealed class StubClientFactory(HttpStatusCode status, string payload = "{}") : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(status, payload)) { BaseAddress = new Uri("http://authentik.invalid/api/v3/") };
    }

    private sealed class StubHandler(HttpStatusCode status, string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload) });
    }
}
