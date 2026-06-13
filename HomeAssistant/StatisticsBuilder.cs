namespace Hariane2Mqtt.HomeAssistant;

/// <summary>
/// Pure (no I/O) builder that turns a set of daily consumption values into the long-term
/// statistics points expected by Home Assistant's Energy dashboard.
///
/// HA computes a day's consumption as <c>sum(D+1 00:00 local) − sum(D 00:00 local)</c>.
/// Therefore the point timestamped at local midnight of day D must carry the cumulative
/// consumption of all days strictly *before* D. The loop below naturally emits both the
/// leading point (= base cumulative, before the first day) and a trailing closing-boundary
/// point at (lastDay+1) 00:00, so each day's volume lands on the correct local calendar day.
/// </summary>
public static class StatisticsBuilder
{
    /// <param name="days">Daily values (date at midnight → m³). May be unordered; deduped by date.</param>
    /// <param name="baseCumulative">Cumulative total of all days strictly before the first day (0 on a full backfill).</param>
    /// <param name="tz">Home Assistant's local timezone, used to place points at local midnight (DST-aware).</param>
    public static (List<HassStatePoint> Points, double NewCumulative, DateOnly? NewLastDay) Build(
        IEnumerable<KeyValuePair<DateTime, float>> days, double baseCumulative, TimeZoneInfo tz)
    {
        var ordered = days
            .Select(kv => (Date: DateOnly.FromDateTime(kv.Key), Value: (double)kv.Value))
            .GroupBy(x => x.Date)
            .Select(g => (Date: g.Key, Value: g.Last().Value))
            .OrderBy(x => x.Date)
            .ToList();

        var points = new List<HassStatePoint>();
        var running = baseCumulative;

        if (ordered.Count == 0)
            return (points, running, null);

        // Leading point: cumulative *before* the first day (re-emits the previous run's closing
        // boundary on an incremental run → idempotent upsert, guaranteeing a continuous series).
        points.Add(new HassStatePoint(LocalMidnight(ordered[0].Date, tz), running, running));

        DateOnly lastDay = ordered[0].Date;
        foreach (var (date, value) in ordered)
        {
            running += value;
            // Point at the *next* day's midnight closes day 'date' with the cumulative through it.
            points.Add(new HassStatePoint(LocalMidnight(date.AddDays(1), tz), running, running));
            lastDay = date;
        }

        return (points, running, lastDay);
    }

    /// <summary>Local midnight of <paramref name="date"/> as a DST-correct, timezone-aware instant.</summary>
    public static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo tz)
    {
        var localMidnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(localMidnight);
        return new DateTimeOffset(localMidnight, offset);
    }
}
