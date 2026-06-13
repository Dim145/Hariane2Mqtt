using Hariane2Mqtt.HomeAssistant;

namespace Hariane2Mqtt.Tests;

public class StatisticsBuilderTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    private static Dictionary<DateTime, float> Days(params (int y, int m, int d, float v)[] items)
        => items.ToDictionary(i => new DateTime(i.y, i.m, i.d), i => i.v);

    // HA reads the cumulative sum at a boundary as the last statistic point at or before it
    // (so missing days simply carry the previous sum forward).
    private static double SumAt(IReadOnlyList<HassStatePoint> pts, DateTimeOffset boundary)
    {
        var applicable = pts.Where(p => p.Start <= boundary).ToList();
        return applicable.Count == 0 ? 0d : applicable[^1].Sum;
    }

    // Consumption HA attributes to a local day = sum(day+1 midnight) - sum(day midnight).
    private static double DayChange(IReadOnlyList<HassStatePoint> pts, DateOnly day)
    {
        var start = StatisticsBuilder.LocalMidnight(day, Paris);
        var end = StatisticsBuilder.LocalMidnight(day.AddDays(1), Paris);
        return SumAt(pts, end) - SumAt(pts, start);
    }

    [Fact]
    public void FullBackfill_StartsAtZero_AndAttributesEachDayToItself()
    {
        var days = Days((2026, 6, 10, 1.5f), (2026, 6, 11, 2.0f), (2026, 6, 12, 0.5f));

        var (points, cumulative, lastDay) = StatisticsBuilder.Build(days, 0d, Paris);

        Assert.Equal(0d, points[0].Sum, 6);
        Assert.Equal(StatisticsBuilder.LocalMidnight(new DateOnly(2026, 6, 10), Paris), points[0].Start);

        // The whole point of the feature: each day's volume lands on that exact day.
        Assert.Equal(1.5d, DayChange(points, new DateOnly(2026, 6, 10)), 6);
        Assert.Equal(2.0d, DayChange(points, new DateOnly(2026, 6, 11)), 6);
        Assert.Equal(0.5d, DayChange(points, new DateOnly(2026, 6, 12)), 6);

        Assert.Equal(4.0d, cumulative, 6);
        Assert.Equal(new DateOnly(2026, 6, 12), lastDay);
        Assert.Equal(4, points.Count); // leading point + one closing boundary per day (boundaries shared)
    }

    [Fact]
    public void Incremental_ReemitsPreviousClosingBoundary_ForContinuity()
    {
        var first = StatisticsBuilder.Build(Days((2026, 6, 10, 1.5f), (2026, 6, 11, 2.0f)), 0d, Paris);

        // Next run continues from the persisted cumulative, starting the day after.
        var second = StatisticsBuilder.Build(Days((2026, 6, 12, 0.5f)), first.NewCumulative, Paris);

        // First point of the incremental run == previous run's closing boundary (same instant + sum) → idempotent upsert.
        Assert.Equal(StatisticsBuilder.LocalMidnight(new DateOnly(2026, 6, 12), Paris), second.Points[0].Start);
        Assert.Equal(first.NewCumulative, second.Points[0].Sum, 6);
        Assert.Equal(first.Points[^1].Sum, second.Points[0].Sum, 6);

        Assert.Equal(0.5d, DayChange(second.Points, new DateOnly(2026, 6, 12)), 6);
        Assert.Equal(4.0d, second.NewCumulative, 6);
    }

    [Fact]
    public void Build_SortsUnorderedInput()
    {
        var days = Days((2026, 6, 12, 0.5f), (2026, 6, 10, 1.5f), (2026, 6, 11, 2.0f));

        var (points, _, _) = StatisticsBuilder.Build(days, 0d, Paris);

        for (var i = 1; i < points.Count; i++)
            Assert.True(points[i].Start > points[i - 1].Start);
    }

    [Fact]
    public void Build_AccumulatesInDouble_NoFloatDrift()
    {
        // 1000 days of 0.001 m³ should total exactly 1.0; a float accumulator visibly drifts here.
        var days = new Dictionary<DateTime, float>();
        for (var i = 0; i < 1000; i++)
            days[new DateTime(2024, 1, 1).AddDays(i)] = 0.001f;

        var (_, cumulative, _) = StatisticsBuilder.Build(days, 0d, Paris);

        Assert.Equal(1.0d, cumulative, 3);
    }

    [Fact]
    public void GapDay_ShowsZeroChange_AndKeepsNeighboursCorrect()
    {
        var days = Days((2026, 6, 10, 1.5f), (2026, 6, 12, 0.5f)); // 2026-06-11 missing

        var (points, _, _) = StatisticsBuilder.Build(days, 0d, Paris);

        Assert.Equal(1.5d, DayChange(points, new DateOnly(2026, 6, 10)), 6);
        Assert.Equal(0.0d, DayChange(points, new DateOnly(2026, 6, 11)), 6); // gap → no consumption
        Assert.Equal(0.5d, DayChange(points, new DateOnly(2026, 6, 12)), 6);
    }

    [Fact]
    public void LocalMidnight_SpringForward_MidnightIsPreTransition()
    {
        // EU spring-forward 2026 is Sunday 29 March (clocks jump 02:00→03:00), so MIDNIGHT the 29th is still winter (+1).
        Assert.Equal(TimeSpan.FromHours(1), StatisticsBuilder.LocalMidnight(new DateOnly(2026, 3, 28), Paris).Offset);
        Assert.Equal(TimeSpan.FromHours(1), StatisticsBuilder.LocalMidnight(new DateOnly(2026, 3, 29), Paris).Offset);
        Assert.Equal(TimeSpan.FromHours(2), StatisticsBuilder.LocalMidnight(new DateOnly(2026, 3, 30), Paris).Offset);

        // The 29th is the short, 23h-long day (midnight 29 → midnight 30).
        var d29 = StatisticsBuilder.LocalMidnight(new DateOnly(2026, 3, 29), Paris).UtcDateTime;
        var d30 = StatisticsBuilder.LocalMidnight(new DateOnly(2026, 3, 30), Paris).UtcDateTime;
        Assert.Equal(TimeSpan.FromHours(23), d30 - d29);
    }

    [Fact]
    public void LocalMidnight_FallBack_MidnightIsPreTransition()
    {
        // EU fall-back 2026 is Sunday 25 October (clocks fall 03:00→02:00), so MIDNIGHT the 25th is still summer (+2).
        Assert.Equal(TimeSpan.FromHours(2), StatisticsBuilder.LocalMidnight(new DateOnly(2026, 10, 24), Paris).Offset);
        Assert.Equal(TimeSpan.FromHours(2), StatisticsBuilder.LocalMidnight(new DateOnly(2026, 10, 25), Paris).Offset);
        Assert.Equal(TimeSpan.FromHours(1), StatisticsBuilder.LocalMidnight(new DateOnly(2026, 10, 26), Paris).Offset);

        // The 25th is the long, 25h-long day (midnight 25 → midnight 26).
        var d25 = StatisticsBuilder.LocalMidnight(new DateOnly(2026, 10, 25), Paris).UtcDateTime;
        var d26 = StatisticsBuilder.LocalMidnight(new DateOnly(2026, 10, 26), Paris).UtcDateTime;
        Assert.Equal(TimeSpan.FromHours(25), d26 - d25);
    }

    [Fact]
    public void DstWeekend_StillAttributesConsumptionToCorrectDay()
    {
        var days = Days((2026, 3, 28, 1.0f), (2026, 3, 29, 2.0f), (2026, 3, 30, 3.0f));

        var (points, _, _) = StatisticsBuilder.Build(days, 0d, Paris);

        Assert.Equal(1.0d, DayChange(points, new DateOnly(2026, 3, 28)), 6);
        Assert.Equal(2.0d, DayChange(points, new DateOnly(2026, 3, 29)), 6); // the 23h DST day
        Assert.Equal(3.0d, DayChange(points, new DateOnly(2026, 3, 30)), 6);
    }

    [Fact]
    public void EmptyInput_ReturnsBaseAndNoPoints()
    {
        var (points, cumulative, lastDay) = StatisticsBuilder.Build(new Dictionary<DateTime, float>(), 42d, Paris);

        Assert.Empty(points);
        Assert.Equal(42d, cumulative, 6);
        Assert.Null(lastDay);
    }
}
