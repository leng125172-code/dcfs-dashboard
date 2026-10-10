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
    private const string Subject = "11111111-2222-3333-4444-555555555555";
    private static readonly string[] AdministratorGroups = ["admin-id"];
    [Fact]
    public async Task MissingApiTokenDoesNotPromoteCookieClaims()
    {
        var directory = new AuthentikIdentityDirectory(new StubClientFactory(HttpStatusCode.ServiceUnavailable), NewCache(), Config());
        var current = await directory.ResolveCurrentAsync(Subject, "User", ["admin-id"], default);
        Assert.False(current.IsAdministrator);
    }

    [Fact]
    public async Task ExpiredSnapshotCannotPreserveAdministratorWhenAuthentikFails()
    {
        var cache = NewCache();
        await cache.SetStringAsync($"whaledeck:authz:{Subject}", JsonSerializer.Serialize(new
        {
            Groups = AdministratorGroups, Administrator = true, ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        }));
        var directory = new AuthentikIdentityDirectory(new StubClientFactory(HttpStatusCode.ServiceUnavailable), cache, Config("test-token"));
        var current = await directory.ResolveCurrentAsync(Subject, "User", ["admin-id"], default);
        Assert.False(current.IsAdministrator);
        Assert.Empty(current.Groups);
    }

    [Fact]
    public async Task ValidSnapshotSurvivesShortAuthentikOutage()
    {
        var cache = NewCache();
        var expires = DateTimeOffset.UtcNow.AddMinutes(30);
        await cache.SetStringAsync($"whaledeck:authz:{Subject}", JsonSerializer.Serialize(new
        {
            Groups = AdministratorGroups, Administrator = true, ExpiresAtUtc = expires
        }));
        var directory = new AuthentikIdentityDirectory(new StubClientFactory(HttpStatusCode.ServiceUnavailable), cache, Config("test-token"));
        var current = await directory.ResolveCurrentAsync(Subject, "User", [], default);
        Assert.True(current.IsAdministrator);
        Assert.Equal(expires, current.PermissionExpiresAtUtc);
    }

    [Fact]
    public async Task ExplicitlyDisabledAccountIsRejected()
    {
        var factory = new StubClientFactory(HttpStatusCode.OK, """{"results":[{"pk":42,"is_active":false,"groups_obj":[{"pk":"admin-id"}]}]}""");
        var directory = new AuthentikIdentityDirectory(factory, NewCache(), Config("test-token"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => directory.ResolveCurrentAsync(Subject, "User", ["admin-id"], default));
    }

    [Fact]
    public async Task AuthoritativeUuidLookupResolvesAdministratorGroup()
    {
        var factory = new StubClientFactory(HttpStatusCode.OK,
            """{"results":[{"pk":42,"is_active":true,"groups_obj":[{"pk":"admin-id","name":"Whale Deck Administrators"}]}]}""");
        var directory = new AuthentikIdentityDirectory(factory, NewCache(), Config("test-token"));

        var current = await directory.ResolveCurrentAsync(Subject, "User", [], default);

        Assert.True(current.IsAdministrator);
        Assert.Equal(["admin-id"], current.Groups);
        Assert.NotNull(factory.LastRequestUri);
        Assert.Equal("/api/v3/core/users/", factory.LastRequestUri.AbsolutePath);
        Assert.Contains($"uuid={Subject}", factory.LastRequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("include_groups=true", factory.LastRequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateUserSerializesAnonymousPayloadUsingItsRuntimeType()
    {
        var factory = new StubClientFactory(HttpStatusCode.Created, """{"pk":42}""");
        var directory = new AuthentikIdentityDirectory(factory, NewCache(), Config("test-token"));

        await directory.ExecuteAsync("create-user", null, new Dictionary<string, string>
        {
            ["username"] = "managed-user",
            ["name"] = "Managed User",
            ["email"] = "managed-user@example.invalid"
        }, default);

        Assert.NotNull(factory.LastRequestBody);
        using var payload = JsonDocument.Parse(factory.LastRequestBody);
        Assert.Equal("managed-user", payload.RootElement.GetProperty("username").GetString());
        Assert.Equal("Managed User", payload.RootElement.GetProperty("name").GetString());
        Assert.True(payload.RootElement.GetProperty("is_active").GetBoolean());
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
        public Uri? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }

        public HttpClient CreateClient(string name) => new(new StubHandler(status, payload, (uri, body) =>
        {
            LastRequestUri = uri;
            LastRequestBody = body;
        }))
        {
            BaseAddress = new Uri("http://authentik.invalid/api/v3/")
        };
    }

    private sealed class StubHandler(HttpStatusCode status, string payload, Action<Uri?, string?> captureRequest) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            captureRequest(request.RequestUri, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status) { Content = new StringContent(payload) };
        }
    }
}
