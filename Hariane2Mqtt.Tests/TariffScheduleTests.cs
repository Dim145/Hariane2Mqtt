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
}
