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
    IConfiguration configuration) : IIdentityDirectory
{
    private static readonly TimeSpan PermissionTtl = TimeSpan.FromHours(2);

    public async Task<CurrentUserDto> ResolveCurrentAsync(string subject, string? name, IReadOnlyCollection<string> claimGroups, CancellationToken cancellationToken)
    {
        var key = $"whaledeck:authz:{subject}";
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            var snapshot = JsonSerializer.Deserialize<PermissionSnapshot>(cached);
            if (snapshot is not null) return new CurrentUserDto(subject, name, snapshot.Groups, snapshot.Administrator, snapshot.ExpiresAtUtc);
        }

        var adminGroup = configuration["Authentication:Authentik:AdministratorGroupId"]
            ?? configuration["Authentication:Authentik:AdministratorGroupName"];
        var resolution = await ResolveGroupsFromAuthentik(subject, claimGroups, cancellationToken);
        var groups = resolution.Groups;
        var administrator = !string.IsNullOrWhiteSpace(adminGroup) && groups.Contains(adminGroup, StringComparer.Ordinal);
        var expires = DateTimeOffset.UtcNow.Add(PermissionTtl);
        var current = new PermissionSnapshot(groups, administrator, expires);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(current),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = PermissionTtl }, cancellationToken);
        return new CurrentUserDto(subject, name, groups, administrator, expires);
    }

    public Task<IReadOnlyCollection<ManagedResourceDto>> ListUsersAsync(CancellationToken cancellationToken) =>
        ListDirectoryAsync("core/users/", "AuthentikUser", cancellationToken);

    public Task<IReadOnlyCollection<ManagedResourceDto>> ListGroupsAsync(CancellationToken cancellationToken) =>
        ListDirectoryAsync("core/groups/", "AuthentikGroup", cancellationToken);

    public Task<IReadOnlyCollection<ManagedResourceDto>> ListSsoApplicationsAsync(CancellationToken cancellationToken) =>
        ListDirectoryAsync("core/applications/", "AuthentikApplication", cancellationToken);

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
            if (user.TryGetProperty("is_active", out var active) && !active.GetBoolean()) return new GroupResolution([], true);
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
        using var response = await Client().GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("results").EnumerateArray().Select(item =>
        {
            var id = item.TryGetProperty("pk", out var pk) ? pk.ToString() : string.Empty;
            var display = item.TryGetProperty("name", out var name) ? name.GetString() : item.TryGetProperty("username", out var username) ? username.GetString() : id;
            var active = !item.TryGetProperty("is_active", out var activeValue) || activeValue.GetBoolean();
            return new ManagedResourceDto(id, display ?? id, type, active ? "Active" : "Disabled", string.Empty, true, new Dictionary<string, string>());
        }).ToArray();
    }

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
