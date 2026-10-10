using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Application.Services;

public sealed class GlobalSearchService(
    IPortalRepository portals,
    IGlobalSearchRepository repository,
    IIdentityDirectory identity)
{
    public async Task<IReadOnlyCollection<GlobalSearchResultDto>> SearchAsync(
        string subject,
        bool administrator,
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 80 || query.Any(char.IsControl))
            throw new ArgumentException("Search query must contain 2 to 80 printable characters.");
        take = Math.Clamp(take, 1, 50);
        var results = new List<GlobalSearchResultDto>();
        var visiblePortals = await portals.ListVisibleAsync(subject, administrator, cancellationToken);
        results.AddRange(visiblePortals.Where(item => Matches(item.Name, query) || Matches(item.Description, query))
            .Take(take)
            .Select(item => new GlobalSearchResultDto($"portal:{item.Id:D}", "Portal", item.Name,
                item.Description ?? item.Url, item.Url, item.IsEnabled ? "Enabled" : "Disabled", IsExternal(item.Url))));

        if (administrator)
        {
            results.AddRange(await repository.SearchAdministrationAsync(query, take, cancellationToken));
            var users = await identity.ListUsersAsync(cancellationToken);
            results.AddRange(users.Where(item => Matches(item.Name, query) || Matches(item.Id, query))
                .Take(take)
                .Select(item => new GlobalSearchResultDto($"user:{item.Id}", "User", item.Name,
                    item.Id, "/identity/users", item.State, false)));
        }

        return results.DistinctBy(item => item.Id).Take(take).ToArray();
    }

    private static bool Matches(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsExternal(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
