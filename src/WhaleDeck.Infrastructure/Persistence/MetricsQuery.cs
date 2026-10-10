using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WhaleDeck.Application.Abstractions;
using WhaleDeck.Application.Models;

namespace WhaleDeck.Infrastructure.Persistence;

public sealed class MetricsQuery(PlatformDbContext db) : IMetricsQuery
{
    public async Task<IReadOnlyCollection<MetricSeriesSnapshotDto>> GetHistoryAsync(
        IReadOnlyCollection<string> kinds,
        int take,
        CancellationToken cancellationToken)
    {
        var normalized = kinds.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).Take(12).ToArray();
        if (normalized.Length == 0) return [];
        var series = await db.MetricSeries.AsNoTracking().Where(item => normalized.Contains(item.MetricKind))
            .OrderBy(item => item.MetricKind).ThenBy(item => item.DimensionsJson).Take(64).ToArrayAsync(cancellationToken);
        var result = new List<MetricSeriesSnapshotDto>(series.Length);
        foreach (var item in series)
        {
            var points = await db.MetricSamples.AsNoTracking().Where(value => value.SeriesId == item.Id)
                .OrderByDescending(value => value.SampledAtUtc).Take(Math.Clamp(take, 1, 600)).OrderBy(value => value.SampledAtUtc)
                .Select(value => new MetricPointDto(value.SampledAtUtc, value.ValueDouble, value.Quality)).ToArrayAsync(cancellationToken);
            var deviceId = "host";
            try
            {
                using var dimensions = JsonDocument.Parse(item.DimensionsJson);
                if (dimensions.RootElement.TryGetProperty("DeviceId", out var device)) deviceId = device.GetString() ?? deviceId;
            }
            catch (JsonException) { }
            result.Add(new MetricSeriesSnapshotDto(item.MetricKind, deviceId, item.Unit, points));
        }
        return result;
    }
}
