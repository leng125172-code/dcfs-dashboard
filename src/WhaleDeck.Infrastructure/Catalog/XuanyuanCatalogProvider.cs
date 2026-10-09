using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;
using WhaleDeck.Domain.Entities;
using WhaleDeck.Infrastructure.Persistence;

namespace WhaleDeck.Infrastructure.Catalog;

public sealed partial class XuanyuanCatalogProvider(IHttpClientFactory httpClientFactory, PlatformDbContext dbContext) : ICatalogProvider
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
            return Deserialize(cached, false);
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
            return new CatalogResultDto(applications, snapshot.FetchedAtUtc, false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            if (cached is not null) return Deserialize(cached, true);
            return new CatalogResultDto(Fallback(), DateTimeOffset.UtcNow, true);
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
            yield return new CatalogApplicationDto(id, name, "轩辕热门容器应用", id, "latest", null, false, null);
        }
    }

    private static CatalogResultDto Deserialize(CatalogSnapshot snapshot, bool stale) => new(
        JsonSerializer.Deserialize<CatalogApplicationDto[]>(snapshot.PayloadJson) ?? [], snapshot.FetchedAtUtc, stale);

    private static CatalogApplicationDto[] Fallback() =>
    [
        new("openresty/openresty", "OpenResty", "高性能 Web 平台与反向代理", "openresty/openresty", "latest", null, false, null),
        new("library/mysql", "MySQL", "关系型数据库", "mysql", "latest", null, false, null),
        new("valkey/valkey", "Valkey", "高性能内存缓存", "valkey/valkey", "latest", null, false, null),
        new("1panel/maxkb", "MaxKB", "企业级 AI 知识库", "1panel/maxkb", "latest", null, false, null),
        new("mongo", "MongoDB", "文档数据库", "mongo", "latest", null, false, null),
        new("postgres", "PostgreSQL", "开源关系型数据库", "postgres", "latest", null, false, null)
    ];

    [GeneratedRegex("href=[\"'](?:https://xuanyuan\\.cloud)?/(?:image|images|detail)/(?<id>[^\"'#?]+)[^\"']*[\"'][^>]*>(?:<[^>]+>)*(?<name>[^<]{2,80})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApplicationLinkPattern();
}
