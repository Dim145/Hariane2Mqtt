using Hariane2Mqtt;

namespace Hariane2Mqtt.Tests;

public class TariffScheduleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    public void Parse_Empty_IsEmpty(string? spec) => Assert.True(TariffSchedule.Parse(spec).IsEmpty);

    [Fact]
    public void Parse_FlatPrice_AppliesToEveryDay()
    {
        var t = TariffSchedule.Parse("4.30");
        Assert.False(t.IsEmpty);
        Assert.Equal(4.30f, t.PriceOn(new DateOnly(2020, 1, 1)), 3);
        Assert.Equal(4.30f, t.PriceOn(new DateOnly(2030, 12, 31)), 3);
    }

    [Fact]
    public void Parse_DatedSchedule_PicksPriceInEffect()
    {
        var t = TariffSchedule.Parse("2023-01-01:3.50,2025-06-01:4.30");
        Assert.Equal(3.50f, t.PriceOn(new DateOnly(2024, 12, 31)), 3); // before the change
        Assert.Equal(3.50f, t.PriceOn(new DateOnly(2025, 5, 31)), 3);  // day before the change
        Assert.Equal(4.30f, t.PriceOn(new DateOnly(2025, 6, 1)), 3);   // change day → new price
        Assert.Equal(4.30f, t.PriceOn(new DateOnly(2026, 1, 1)), 3);   // after
    }

    [Fact]
    public void PriceOn_BeforeFirstDatedTariff_IsZero()
    {
        var t = TariffSchedule.Parse("2025-06-01:4.30");
        Assert.Equal(0f, t.PriceOn(new DateOnly(2025, 5, 31)), 3);
        Assert.Equal(4.30f, t.PriceOn(new DateOnly(2025, 6, 1)), 3);
    }

    [Fact]
    public void Parse_UnsortedEntries_AreOrdered()
    {
        var t = TariffSchedule.Parse("2025-06-01:4.30,2023-01-01:3.50");
        Assert.Equal(3.50f, t.PriceOn(new DateOnly(2024, 1, 1)), 3);
        Assert.Equal(4.30f, t.PriceOn(new DateOnly(2025, 7, 1)), 3);
    }

    [Fact]
    public void Parse_DetectsFlatVsDated()
    {
        Assert.Equal(4.30f, TariffSchedule.Parse("4.30").FlatPrice!.Value, 3);
        Assert.Null(TariffSchedule.Parse("2023-01-01:3.50,2025-06-01:4.30").FlatPrice);
        Assert.Null(TariffSchedule.Parse("").FlatPrice);
    }

    [Fact]
    public void RecordFlatChange_StampsTheUserScenario()
    {
        // 3.50 set initially, then 4.50 on 18/06, then 5.50 on 20/06 — just changing the flat number.
        var h = new List<TariffEntry>();
        h = TariffSchedule.RecordFlatChange(h, 3.50f, new DateOnly(2026, 6, 16)); // first → whole history
        h = TariffSchedule.RecordFlatChange(h, 3.50f, new DateOnly(2026, 6, 17)); // unchanged → no new entry
        h = TariffSchedule.RecordFlatChange(h, 4.50f, new DateOnly(2026, 6, 18)); // change → from 18/06
        h = TariffSchedule.RecordFlatChange(h, 5.50f, new DateOnly(2026, 6, 20)); // change → from 20/06

        Assert.Equal(3, h.Count);
        Assert.Equal(DateOnly.MinValue, h[0].Date);

        var t = TariffSchedule.FromEntries(h);
        Assert.Equal(3.50f, t.PriceOn(new DateOnly(2020, 1, 1)), 3);  // old history
        Assert.Equal(3.50f, t.PriceOn(new DateOnly(2026, 6, 17)), 3); // < 18/06
        Assert.Equal(4.50f, t.PriceOn(new DateOnly(2026, 6, 18)), 3); // [18/06, 20/06[
        Assert.Equal(4.50f, t.PriceOn(new DateOnly(2026, 6, 19)), 3);
        Assert.Equal(5.50f, t.PriceOn(new DateOnly(2026, 6, 20)), 3); // >= 20/06
        Assert.Equal(5.50f, t.PriceOn(new DateOnly(2027, 1, 1)), 3);
    }
}
