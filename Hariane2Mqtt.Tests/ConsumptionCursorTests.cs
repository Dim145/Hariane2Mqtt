using Hariane2Mqtt;

namespace Hariane2Mqtt.Tests;

public class ConsumptionCursorTests
{
    [Fact]
    public void PlanWindow_FirstRun_NeedsFullHistory()
    {
        var (full, from) = ConsumptionCursor.PlanWindow(null, importStats: false, costEnabled: false);
        Assert.True(full);
        Assert.Equal(DateTime.MinValue, from);
    }

    [Fact]
    public void PlanWindow_MigratedState_NeedsFullHistory_WhenStatsNotYetImported()
    {
        var state = new AppState { CumulativeTotal = 10, LastDataDate = new DateOnly(2026, 6, 1), StatisticsImported = false };
        var (full, _) = ConsumptionCursor.PlanWindow(state, importStats: true, costEnabled: false);
        Assert.True(full);
    }

    [Fact]
    public void PlanWindow_Incremental_WhenStatsAlreadyImported()
    {
        var state = new AppState { CumulativeTotal = 10, LastDataDate = new DateOnly(2026, 6, 1), StatisticsImported = true };
        var (full, from) = ConsumptionCursor.PlanWindow(state, importStats: true, costEnabled: false);
        Assert.False(full);
        Assert.Equal(new DateTime(2026, 6, 2), from); // the day after the last recorded day
    }

    [Fact]
    public void PlanWindow_Incremental_WhenImportDisabled_EvenIfNotYetImported()
    {
        var state = new AppState { CumulativeTotal = 10, LastDataDate = new DateOnly(2026, 6, 1), StatisticsImported = false };
        var (full, from) = ConsumptionCursor.PlanWindow(state, importStats: false, costEnabled: false);
        Assert.False(full);
        Assert.Equal(new DateTime(2026, 6, 2), from);
    }

    [Fact]
    public void PlanWindow_CostNewlyEnabled_NeedsFullHistory()
    {
        // water already imported, but cost was never computed → rebuild to get historical cost
        var state = new AppState { CumulativeTotal = 10, LastDataDate = new DateOnly(2026, 6, 1), StatisticsImported = true, CostImported = false };
        var (full, _) = ConsumptionCursor.PlanWindow(state, importStats: true, costEnabled: true);
        Assert.True(full);
    }

    [Fact]
    public void PlanWindow_Incremental_WhenCostAlreadyImported()
    {
        var state = new AppState { CumulativeTotal = 10, LastDataDate = new DateOnly(2026, 6, 1), StatisticsImported = true, CostImported = true };
        var (full, _) = ConsumptionCursor.PlanWindow(state, importStats: true, costEnabled: true);
        Assert.False(full);
    }

    [Fact]
    public void Advance_AddsSeriesToBase_AndTracksLastDay()
    {
        var series = new Dictionary<DateTime, float>
        {
            { new DateTime(2026, 6, 2), 1.5f },
            { new DateTime(2026, 6, 3), 2.0f },
        };

        var (cumulative, lastDay) = ConsumptionCursor.Advance(10.0, series, null);

        Assert.Equal(13.5, cumulative, 6);
        Assert.Equal(new DateOnly(2026, 6, 3), lastDay);
    }

    [Fact]
    public void Advance_EmptySeries_KeepsBaseAndPreviousLastDay()
    {
        var previous = new AppState { CumulativeTotal = 10, LastDataDate = new DateOnly(2026, 6, 1), StatisticsImported = true };

        var (cumulative, lastDay) = ConsumptionCursor.Advance(10.0, new Dictionary<DateTime, float>(), previous);

        Assert.Equal(10.0, cumulative, 6);
        Assert.Equal(new DateOnly(2026, 6, 1), lastDay);
    }
}
