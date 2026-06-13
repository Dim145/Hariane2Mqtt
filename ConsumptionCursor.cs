namespace Hariane2Mqtt;

/// <summary>
/// Pure, testable decisions about the cumulative-consumption cursor shared by the MQTT total
/// and the Home Assistant statistics import.
/// </summary>
public static class ConsumptionCursor
{
    /// <summary>
    /// Decides the fetch window. A full history walk is needed on the first run ever, or to seed
    /// HA statistics the first time the energy import runs for an already-tracked meter.
    /// Otherwise we fetch incrementally from the day after the last recorded day.
    /// </summary>
    public static (bool FullHistory, DateTime From) PlanWindow(AppState? state, bool importStats)
    {
        var needFull = state is null || (importStats && !state.StatisticsImported);
        if (needFull)
            return (true, DateTime.MinValue);

        return (false, state!.LastDataDate.ToDateTime(TimeOnly.MinValue).AddDays(1));
    }

    /// <summary>
    /// Advances the cursor: new cumulative total (in double, to avoid float drift) and the new
    /// last-data day. Falls back to the previous last day when no new data was fetched.
    /// </summary>
    public static (double Cumulative, DateOnly? LastDay) Advance(
        double baseCumulative, IReadOnlyDictionary<DateTime, float> series, AppState? previous)
    {
        var cumulative = baseCumulative;
        DateTime? max = null;

        foreach (var (day, value) in series)
        {
            cumulative += value;
            if (max is null || day > max) max = day;
        }

        var lastDay = max.HasValue ? DateOnly.FromDateTime(max.Value) : previous?.LastDataDate;
        return (cumulative, lastDay);
    }
}
