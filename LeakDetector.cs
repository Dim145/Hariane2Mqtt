namespace Hariane2Mqtt;

/// <summary>
/// Simple, configurable heuristic: a leak is suspected when daily consumption never drops below
/// <c>thresholdM3</c> over the last <c>minDays</c> days — i.e. the meter shows a continuous
/// baseline flow that never rests. Thresholds are household-specific and meant to be tuned.
/// </summary>
public static class LeakDetector
{
    public static (bool Suspected, float MinDaily, int Days) Evaluate(
        IReadOnlyDictionary<DateTime, float> daily, int minDays, float thresholdM3)
    {
        var recent = daily
            .OrderByDescending(kv => kv.Key)
            .Take(minDays)
            .Select(kv => kv.Value)
            .ToList();

        if (minDays <= 0 || recent.Count < minDays)
            return (false, recent.Count > 0 ? recent.Min() : 0f, recent.Count);

        var min = recent.Min();
        return (min >= thresholdM3, min, recent.Count);
    }
}
