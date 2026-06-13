using Hariane2Mqtt;

namespace Hariane2Mqtt.Tests;

public class LeakDetectorTests
{
    private static Dictionary<DateTime, float> Days(params float[] values)
    {
        var d = new Dictionary<DateTime, float>();
        var start = new DateTime(2026, 6, 1);
        for (var i = 0; i < values.Length; i++)
            d[start.AddDays(i)] = values[i];
        return d;
    }

    [Fact]
    public void Suspected_WhenEveryRecentDayStaysAboveThreshold()
    {
        // last 3 days = 0.2, 0.15, 0.12 — all >= 0.1, never resting → suspected
        var (suspected, min, days) = LeakDetector.Evaluate(Days(0.0f, 0.2f, 0.15f, 0.12f), minDays: 3, thresholdM3: 0.1f);

        Assert.True(suspected);
        Assert.Equal(0.12f, min, 3);
        Assert.Equal(3, days);
    }

    [Fact]
    public void NotSuspected_WhenADryDayIsWithinWindow()
    {
        // last 3 days = 0.2, 0.0, 0.15 — a 0.0 day means the meter rested → not a leak
        var (suspected, min, _) = LeakDetector.Evaluate(Days(0.3f, 0.2f, 0.0f, 0.15f), minDays: 3, thresholdM3: 0.1f);

        Assert.False(suspected);
        Assert.Equal(0.0f, min, 3);
    }

    [Fact]
    public void NotSuspected_WhenNotEnoughHistory()
    {
        var (suspected, _, days) = LeakDetector.Evaluate(Days(0.5f, 0.5f), minDays: 3, thresholdM3: 0.1f);

        Assert.False(suspected);
        Assert.Equal(2, days);
    }
}
