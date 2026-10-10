using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Infrastructure.Identity;

public sealed class AuthentikIdentityDirectory(
    IHttpClientFactory httpClientFactory,
    IDistributedCache cache,
    IConfiguration configuration) : IIdentityDirectory, IIdentityManager
{
    private static readonly TimeSpan PermissionTtl = TimeSpan.FromHours(2);

    public async Task<CurrentUserDto> ResolveCurrentAsync(string subject, string? name, IReadOnlyCollection<string> claimGroups, CancellationToken cancellationToken)
    {
        var key = $"whaledeck:authz:{subject}";
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            var snapshot = JsonSerializer.Deserialize<PermissionSnapshot>(cached);
            if (snapshot is not null && snapshot.ExpiresAtUtc > DateTimeOffset.UtcNow)
                return new CurrentUserDto(subject, name, snapshot.Groups, snapshot.Administrator, snapshot.ExpiresAtUtc);
        }

        var adminGroup = configuration["Authentication:Authentik:AdministratorGroupId"]
            ?? configuration["Authentication:Authentik:AdministratorGroupName"];
        var resolution = await ResolveGroupsFromAuthentik(subject, claimGroups, cancellationToken);
        var groups = resolution.Groups;
        var administrator = !string.IsNullOrWhiteSpace(adminGroup) && groups.Contains(adminGroup, StringComparer.Ordinal);
        var ttl = resolution.Authoritative || configuration.GetValue<bool>("Authentication:Authentik:AllowClaimFallback")
            ? PermissionTtl : TimeSpan.FromSeconds(15);
        var expires = DateTimeOffset.UtcNow.Add(ttl);
        var current = new PermissionSnapshot(groups, administrator, expires);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(current),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
        return new CurrentUserDto(subject, name, groups, administrator, expires);
    }

    public Task<IReadOnlyCollection<ManagedResourceDto>> ListUsersAsync(CancellationToken cancellationToken) =>
        ListDirectoryAsync("core/users/", "AuthentikUser", cancellationToken);

    public Task<IReadOnlyCollection<ManagedResourceDto>> ListGroupsAsync(CancellationToken cancellationToken) =>
        ListDirectoryAsync("core/groups/", "AuthentikGroup", cancellationToken);

    public Task<IReadOnlyCollection<ManagedResourceDto>> ListSsoApplicationsAsync(CancellationToken cancellationToken) =>
        ListDirectoryAsync("core/applications/", "AuthentikApplication", cancellationToken);

    public async Task ExecuteAsync(string action, string? resourceId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "create-user":
            {
                var username = Required(parameters, "username", 1, 150);
                var payload = new
                {
                    username,
                    name = Optional(parameters, "name", 150) ?? username,
                    email = Optional(parameters, "email", 254) ?? string.Empty,
                    is_active = true
                };
                using var created = await SendAsync(HttpMethod.Post, "core/users/", payload, cancellationToken);
                using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync(cancellationToken));
                var id = document.RootElement.GetProperty("pk").ToString();
                if (parameters.TryGetValue("password", out var password))
                    await SendAndDisposeAsync(HttpMethod.Post, $"core/users/{Uri.EscapeDataString(id)}/set_password/", new { password }, cancellationToken);
                return;
            }
            case "update-user":
                await SendAndDisposeAsync(HttpMethod.Patch, $"core/users/{ResourceId(resourceId)}/", new
                {
                    name = Optional(parameters, "name", 150),
                    email = Optional(parameters, "email", 254)
                }, cancellationToken);
                return;
            case "disable-user":
                await SendAndDisposeAsync(HttpMethod.Patch, $"core/users/{ResourceId(resourceId)}/", new { is_active = false }, cancellationToken);
                return;
            case "create-sso":
            {
                var slug = Required(parameters, "slug", 1, 80);
                var providerType = parameters.TryGetValue("providerType", out var type) ? type : "oauth2";
                if (providerType != "oauth2") throw new InvalidOperationException("Only the approved OAuth2/OIDC provider type is currently supported.");
                var providerPayload = new
                {
                    name = Required(parameters, "name", 1, 150),
                    authorization_flow = Required(parameters, "authorizationFlowId", 1, 100),
                    invalidation_flow = Required(parameters, "invalidationFlowId", 1, 100),
                    client_type = "confidential",
                    grant_types = new[] { "authorization_code", "refresh_token" },
                    redirect_uris = new[]
                    {
                        new
                        {
                            matching_mode = "strict",
                            url = ApprovedRedirectUri(parameters),
                            redirect_uri_type = "authorization"
                        }
                    },
                    sub_mode = "hashed_user_id"
                };
                using var provider = await SendAsync(HttpMethod.Post, "providers/oauth2/", providerPayload, cancellationToken);
                using var document = JsonDocument.Parse(await provider.Content.ReadAsStringAsync(cancellationToken));
                var providerId = document.RootElement.GetProperty("pk").ToString();
                await SendAndDisposeAsync(HttpMethod.Post, "core/applications/", new
                {
                    name = providerPayload.name,
                    slug,
                    provider = providerId,
                    policy_engine_mode = "any"
                }, cancellationToken);
                return;
            }
            case "update-sso":
                await SendAndDisposeAsync(HttpMethod.Patch, $"core/applications/{ResourceId(resourceId)}/", new
                {
                    name = Optional(parameters, "name", 150),
                    open_in_new_tab = parameters.TryGetValue("openInNewTab", out var open) && bool.TryParse(open, out var parsed) && parsed
                }, cancellationToken);
                return;
            default: throw new InvalidOperationException("The Authentik management action is unsupported.");
        }
    }

    private async Task<GroupResolution> ResolveGroupsFromAuthentik(string subject, IReadOnlyCollection<string> fallback, CancellationToken cancellationToken)
    {
        var token = configuration["Authentication:Authentik:ApiToken"];
        var allowClaimFallback = configuration.GetValue("Authentication:Authentik:AllowClaimFallback", false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new GroupResolution(allowClaimFallback ? fallback.ToArray() : [], false);
        }
        try
        {
            using var response = await Client().GetAsync($"core/users/{Uri.EscapeDataString(subject)}/", cancellationToken);
            if (!response.IsSuccessStatusCode) return new GroupResolution([], false);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var user = document.RootElement;
            if (user.TryGetProperty("is_active", out var active) && !active.GetBoolean())
                throw new UnauthorizedAccessException("The Authentik account is disabled.");
            if (!user.TryGetProperty("groups_obj", out var groups)) return new GroupResolution([], true);
            return new GroupResolution(groups.EnumerateArray()
                .Select(group => group.TryGetProperty("pk", out var pk) ? pk.GetString() : group.GetProperty("name").GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).ToArray(), true);
        }
        catch (HttpRequestException) when (!allowClaimFallback)
        {
            return new GroupResolution([], false);
        }
        catch (HttpRequestException) when (allowClaimFallback)
        {
            return new GroupResolution(fallback.ToArray(), false);
        }
    }

    private async Task<IReadOnlyCollection<ManagedResourceDto>> ListDirectoryAsync(string path, string type, CancellationToken cancellationToken)
    {
        var results = new List<ManagedResourceDto>();
        string? next = path;
        for (var page = 0; page < 20 && next is not null; page++)
        {
            using var response = await Client().GetAsync(next, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            results.AddRange(document.RootElement.GetProperty("results").EnumerateArray().Select(item =>
            {
                var id = item.TryGetProperty("pk", out var pk) ? pk.ToString() : string.Empty;
                var display = item.TryGetProperty("name", out var name) ? name.GetString() : item.TryGetProperty("username", out var username) ? username.GetString() : id;
                var active = !item.TryGetProperty("is_active", out var activeValue) || activeValue.GetBoolean();
                return new ManagedResourceDto(id, display ?? id, type, active ? "Active" : "Disabled", string.Empty, true, new Dictionary<string, string>());
            }));
            next = document.RootElement.TryGetProperty("pagination", out var pagination) &&
                   pagination.TryGetProperty("next", out var nextValue) && nextValue.ValueKind == JsonValueKind.Number && nextValue.GetInt32() > 0
                ? AppendPage(path, nextValue.GetInt32()) : null;
        }
        return results;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        var response = await Client().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task SendAndDisposeAsync(HttpMethod method, string path, object payload, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, path, payload, cancellationToken);
    }

    private static string ResourceId(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100
        ? Uri.EscapeDataString(value) : throw new InvalidOperationException("The Authentik resource id is invalid.");
    private static string Required(IReadOnlyDictionary<string, string> values, string key, int minimum, int maximum) =>
        values.TryGetValue(key, out var value) && value.Trim().Length >= minimum && value.Trim().Length <= maximum
            ? value.Trim() : throw new InvalidOperationException($"The Authentik field is invalid: {key}");
    private static string? Optional(IReadOnlyDictionary<string, string> values, string key, int maximum) =>
        values.TryGetValue(key, out var value) && value.Trim().Length <= maximum ? value.Trim() : null;
    private static string ApprovedRedirectUri(IReadOnlyDictionary<string, string> values)
    {
        var value = Required(values, "redirectUri", 1, 2048);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp ||
            uri.Host is not ("192.168.22.19" or "192.168.100.13" or "127.0.0.1" or "localhost") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("The SSO redirect URI is not approved.");
        return uri.AbsoluteUri;
    }
    private static string AppendPage(string path, int page) => path + (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + $"page={page}";

    private HttpClient Client()
    {
        var client = httpClientFactory.CreateClient("Authentik");
        var token = configuration["Authentication:Authentik:ApiToken"];
        if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private sealed record PermissionSnapshot(string[] Groups, bool Administrator, DateTimeOffset ExpiresAtUtc);
    private sealed record GroupResolution(string[] Groups, bool Authoritative);
}
