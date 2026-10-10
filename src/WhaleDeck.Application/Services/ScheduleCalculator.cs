using System.Globalization;

namespace WhaleDeck.Application.Services;

public static class ScheduleCalculator
{
    public static DateTimeOffset NextUtc(string kind, string expression, string timezone, DateTimeOffset afterUtc)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        return kind switch
        {
            "Interval" => afterUtc.AddMinutes(ParseInterval(expression)),
            "Daily" => NextDaily(expression, zone, afterUtc),
            "Cron" => NextCron(expression, zone, afterUtc),
            _ => throw new ArgumentException("Unsupported schedule kind.")
        };
    }

    private static int ParseInterval(string expression) =>
        int.TryParse(expression, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes is >= 1 and <= 10080
            ? minutes : throw new ArgumentException("Interval must be between 1 and 10080 minutes.");

    private static DateTimeOffset NextDaily(string expression, TimeZoneInfo zone, DateTimeOffset afterUtc)
    {
        if (!TimeOnly.TryParseExact(expression, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new ArgumentException("Daily schedule must use HH:mm.");
        var local = TimeZoneInfo.ConvertTime(afterUtc, zone);
        var candidate = new DateTime(local.Year, local.Month, local.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);
        if (candidate <= local.DateTime) candidate = candidate.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(candidate, zone);
    }

    private static DateTimeOffset NextCron(string expression, TimeZoneInfo zone, DateTimeOffset afterUtc)
    {
        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5) throw new ArgumentException("Cron expression must have five fields.");
        var minute = CronField.Parse(fields[0], 0, 59);
        var hour = CronField.Parse(fields[1], 0, 23);
        var day = CronField.Parse(fields[2], 1, 31);
        var month = CronField.Parse(fields[3], 1, 12);
        var weekday = CronField.Parse(fields[4], 0, 7, normalizeSevenToZero: true);
        var candidateUtc = afterUtc.UtcDateTime.AddMinutes(1);
        candidateUtc = new DateTime(candidateUtc.Year, candidateUtc.Month, candidateUtc.Day, candidateUtc.Hour, candidateUtc.Minute, 0, DateTimeKind.Utc);
        for (var iteration = 0; iteration < 527040; iteration++, candidateUtc = candidateUtc.AddMinutes(1))
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(candidateUtc, zone);
            if (minute.Contains(local.Minute) && hour.Contains(local.Hour) && day.Contains(local.Day) &&
                month.Contains(local.Month) && weekday.Contains((int)local.DayOfWeek))
                return new DateTimeOffset(candidateUtc);
        }
        throw new ArgumentException("Cron expression does not produce a run within one year.");
    }

    private sealed class CronField(HashSet<int> values)
    {
        public bool Contains(int value) => values.Contains(value);

        public static CronField Parse(string source, int minimum, int maximum, bool normalizeSevenToZero = false)
        {
            var values = new HashSet<int>();
            foreach (var part in source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var stepParts = part.Split('/', 2);
                var step = stepParts.Length == 2 && int.TryParse(stepParts[1], CultureInfo.InvariantCulture, out var parsedStep) && parsedStep > 0
                    ? parsedStep : stepParts.Length == 1 ? 1 : throw new ArgumentException("Cron step is invalid.");
                var range = stepParts[0];
                int start;
                int end;
                if (range == "*")
                {
                    start = minimum;
                    end = maximum;
                }
                else if (range.Contains('-', StringComparison.Ordinal))
                {
                    var bounds = range.Split('-', 2);
                    if (!int.TryParse(bounds[0], CultureInfo.InvariantCulture, out start) ||
                        !int.TryParse(bounds[1], CultureInfo.InvariantCulture, out end))
                        throw new ArgumentException("Cron range is invalid.");
                }
                else if (int.TryParse(range, CultureInfo.InvariantCulture, out var single))
                {
                    start = single;
                    end = single;
                }
                else throw new ArgumentException("Cron field is invalid.");

                if (start < minimum || end > maximum || start > end) throw new ArgumentException("Cron value is out of range.");
                for (var value = start; value <= end; value += step) values.Add(normalizeSevenToZero && value == 7 ? 0 : value);
            }
            if (values.Count == 0) throw new ArgumentException("Cron field is empty.");
            return new CronField(values);
        }
    }
}
