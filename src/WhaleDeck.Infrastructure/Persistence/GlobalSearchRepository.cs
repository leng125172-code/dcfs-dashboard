using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class GlobalSearchRepository(PlatformDbContext db) : IGlobalSearchRepository
{
    public async Task<IReadOnlyCollection<GlobalSearchResultDto>> SearchAdministrationAsync(
        string query,
        int take,
        CancellationToken cancellationToken)
    {
        var pattern = $"%{EscapeLike(query)}%";
        var resources = await db.ManagedResources.AsNoTracking()
            .Where(item => EF.Functions.ILike(item.DisplayName, pattern, "\\") || EF.Functions.ILike(item.ExternalId, pattern, "\\"))
            .OrderBy(item => item.DisplayName).Take(take).ToArrayAsync(cancellationToken);
        var applications = await db.ApplicationInstallations.AsNoTracking()
            .Where(item => EF.Functions.ILike(item.DisplayName, pattern, "\\") || EF.Functions.ILike(item.CatalogAppId, pattern, "\\"))
            .OrderBy(item => item.DisplayName).Take(take).ToArrayAsync(cancellationToken);
        var jobs = await db.OperationJobs.AsNoTracking()
            .Where(item => EF.Functions.ILike(item.JobType, pattern, "\\") || EF.Functions.ILike(item.State, pattern, "\\"))
            .OrderByDescending(item => item.CreatedAtUtc).Take(take).ToArrayAsync(cancellationToken);

        var results = new List<GlobalSearchResultDto>(resources.Length + applications.Length + jobs.Length);
        results.AddRange(resources.Select(item => new GlobalSearchResultDto($"resource:{item.Id:D}", item.ResourceType,
            item.DisplayName, item.ExternalId, ResourceUrl(item.ResourceType), item.ProtectionLevel, false)));
        results.AddRange(applications.Select(item => new GlobalSearchResultDto($"application:{item.Id:D}", "Application",
            item.DisplayName, item.InstalledVersion, $"/applications/{item.Id:D}", item.State, false)));
        results.AddRange(jobs.Select(item => new GlobalSearchResultDto($"job:{item.Id:D}", "Job", item.JobType,
            item.Id.ToString("D"), "/jobs", item.State, false)));
        return results.Take(take).ToArray();
    }

    private static string ResourceUrl(string type) => type switch
    {
        "Container" => "/containers",
        "Image" => "/docker/images",
        "DockerNetwork" => "/docker/networks",
        "DockerVolume" => "/docker/volumes",
        "Database" or "DatabaseInstance" => "/databases",
        "ComposeApplication" => "/applications",
        "SystemdUnit" => "/systemd",
        _ => "/"
    };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
