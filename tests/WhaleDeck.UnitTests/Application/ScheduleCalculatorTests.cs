using WhaleDeck.Application.Services;

namespace WhaleDeck.UnitTests.Application;

public sealed class ScheduleCalculatorTests
{
    [Fact]
    public void CalculatesCronInConfiguredTimezone()
    {
        var after = new DateTimeOffset(2026, 10, 10, 11, 59, 0, TimeSpan.Zero);
        var next = ScheduleCalculator.NextUtc("Cron", "0 20 * * 0", "Asia/Shanghai", after);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void SupportsRangesListsAndSteps()
    {
        var after = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var next = ScheduleCalculator.NextUtc("Cron", "*/15 8-9 * * 1-5", "UTC", after);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void RejectsOutOfRangeCronFields() =>
        Assert.Throws<ArgumentException>(() => ScheduleCalculator.NextUtc("Cron", "61 * * * *", "UTC", DateTimeOffset.UtcNow));
}
