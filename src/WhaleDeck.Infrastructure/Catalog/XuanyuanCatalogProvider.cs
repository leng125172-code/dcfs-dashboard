using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Domain.Entities;
using WhaleDeck.Infrastructure.Persistence;

namespace WhaleDeck.Infrastructure.Catalog;

public sealed partial class XuanyuanCatalogProvider(
    IHttpClientFactory httpClientFactory,
    PlatformDbContext dbContext,
    IAgentGateway agent) : ICatalogProvider
{
    private const string Provider = "xuanyuan";

    public async Task<CatalogResultDto> GetPopularAsync(CancellationToken cancellationToken)
    {
        var cached = await dbContext.CatalogSnapshots.AsNoTracking()
            .Where(item => item.Provider == Provider)
            .OrderByDescending(item => item.FetchedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (cached is not null && cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return await WithInstallationStateAsync(Deserialize(cached, false), cancellationToken);
        }

        try
        {
            var html = await httpClientFactory.CreateClient("Xuanyuan").GetStringAsync("popular", cancellationToken);
            var applications = Parse(html).Take(6).ToArray();
            if (applications.Length == 0) throw new InvalidOperationException("Xuanyuan popular page did not contain recognizable applications.");
            var snapshot = new CatalogSnapshot
            {
                Provider = Provider,
                PayloadJson = JsonSerializer.Serialize(applications),
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
            };
            dbContext.CatalogSnapshots.Add(snapshot);
            await dbContext.SaveChangesAsync(cancellationToken);
            return await WithInstallationStateAsync(new CatalogResultDto(applications, snapshot.FetchedAtUtc, false), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            if (cached is not null) return await WithInstallationStateAsync(Deserialize(cached, true), cancellationToken);
            return await WithInstallationStateAsync(new CatalogResultDto(Fallback(), DateTimeOffset.UtcNow, true), cancellationToken);
        }
    }

    private static IEnumerable<CatalogApplicationDto> Parse(string html)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ApplicationLinkPattern().Matches(html))
        {
            var id = match.Groups["id"].Value.Trim('/');
            if (!seen.Add(id)) continue;
            var name = System.Net.WebUtility.HtmlDecode(match.Groups["name"].Value).Trim();
            if (string.IsNullOrWhiteSpace(name)) name = id;
            yield return new CatalogApplicationDto(ToSlug(id), name, "轩辕热门容器应用", id, "latest", null, false, null);
        }
    }

    private static CatalogResultDto Deserialize(CatalogSnapshot snapshot, bool stale) => new(
        (JsonSerializer.Deserialize<CatalogApplicationDto[]>(snapshot.PayloadJson) ?? [])
            .Select(item => item with { Id = ToSlug(item.Id) })
            .ToArray(),
        snapshot.FetchedAtUtc,
        stale);

    private static CatalogApplicationDto[] Fallback() =>
    [
        new("openresty", "OpenResty", "高性能 Web 平台与反向代理", "openresty/openresty", "latest", null, false, null),
        new("mysql", "MySQL", "关系型数据库", "mysql", "latest", null, false, null),
        new("valkey", "Valkey", "高性能内存缓存", "valkey/valkey", "latest", null, false, null),
        new("maxkb", "MaxKB", "企业级 AI 知识库", "1panel/maxkb", "latest", null, false, null),
        new("mongo", "MongoDB", "文档数据库", "mongo", "latest", null, false, null),
        new("postgres", "PostgreSQL", "开源关系型数据库", "postgres", "latest", null, false, null)
    ];

    private async Task<CatalogResultDto> WithInstallationStateAsync(CatalogResultDto catalog, CancellationToken cancellationToken)
    {
        try
        {
            var installed = await agent.ListResourcesAsync("applications", cancellationToken);
            var applications = catalog.Applications.Select(item =>
            {
                var resource = installed.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, $"application:{item.Id}", StringComparison.OrdinalIgnoreCase));
                return resource is null ? item : item with { Installed = true, State = resource.State };
            }).ToArray();
            return catalog with { Applications = applications };
        }
        catch (Exception exception) when (exception is RpcException or IOException or SocketException)
        {
            return catalog;
        }
    }

    private static string ToSlug(string value)
    {
        var slug = SlugCharacterPattern().Replace(value.ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "application" : slug;
    }

    [GeneratedRegex("href=[\"'](?:https://xuanyuan\\.cloud)?/(?:image|images|detail)/(?<id>[^\"'#?]+)[^\"']*[\"'][^>]*>(?:<[^>]+>)*(?<name>[^<]{2,80})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApplicationLinkPattern();

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex SlugCharacterPattern();
}
