using System.Globalization;
using Hariane2Mqtt;

namespace Hariane2Mqtt.Tests;

public class StateStoreTests
{
    private static DirectoryInfo TempDir() => Directory.CreateTempSubdirectory("hariane-test-");

    private const string Default = StateStore.DefaultStateFile;

    [Fact]
    public void Save_Then_Load_RoundTrips()
    {
        var dir = TempDir();
        try
        {
            StateStore.Save(dir.FullName, Default, new AppState
            {
                CumulativeTotal = 1234.567,
                LastDataDate = new DateOnly(2026, 6, 12),
                StatisticsImported = true,
            });

            var loaded = StateStore.LoadOrMigrate(dir.FullName, Default);

            Assert.NotNull(loaded);
            Assert.Equal(1234.567, loaded!.CumulativeTotal, 6);
            Assert.Equal(new DateOnly(2026, 6, 12), loaded.LastDataDate);
            Assert.True(loaded.StatisticsImported);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void LoadOrMigrate_FromLegacyTextFile_SeedsCursor()
    {
        var dir = TempDir();
        try
        {
            // legacy format written by older versions: "<total>\n<lastDate (InvariantCulture)>"
            var lastDate = new DateTime(2026, 6, 12);
            File.WriteAllText(
                Path.Combine(dir.FullName, "hariane2mqtt_total_consumption.txt"),
                $"42.5\n{lastDate.ToString(CultureInfo.InvariantCulture)}");

            var loaded = StateStore.LoadOrMigrate(dir.FullName, Default);

            Assert.NotNull(loaded);
            Assert.Equal(42.5, loaded!.CumulativeTotal, 6);
            Assert.Equal(new DateOnly(2026, 6, 12), loaded.LastDataDate);
            Assert.False(loaded.StatisticsImported); // history not yet in HA statistics
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void LoadOrMigrate_ReturnsNull_WhenNothingExists()
    {
        var dir = TempDir();
        try { Assert.Null(StateStore.LoadOrMigrate(dir.FullName, Default)); }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void JsonState_TakesPrecedence_OverLegacyFile()
    {
        var dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "hariane2mqtt_total_consumption.txt"), "1.0\n01/01/2020 00:00:00");
            StateStore.Save(dir.FullName, Default, new AppState
            {
                CumulativeTotal = 999,
                LastDataDate = new DateOnly(2026, 1, 1),
                StatisticsImported = true,
            });

            var loaded = StateStore.LoadOrMigrate(dir.FullName, Default);

            Assert.Equal(999, loaded!.CumulativeTotal, 6);
            Assert.True(loaded.StatisticsImported);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void StateFileFor_UsesDefaultForSingle_AndSuffixForMultiple()
    {
        Assert.Equal("hariane2mqtt_state.json", StateStore.StateFileFor("123", multi: false));
        Assert.Equal("hariane2mqtt_state_123.json", StateStore.StateFileFor("123", multi: true));
    }

    [Fact]
    public void MultiContract_StatesAreIsolated()
    {
        var dir = TempDir();
        try
        {
            StateStore.Save(dir.FullName, StateStore.StateFileFor("a", multi: true),
                new AppState { CumulativeTotal = 1, LastDataDate = new DateOnly(2026, 1, 1), StatisticsImported = true });
            StateStore.Save(dir.FullName, StateStore.StateFileFor("b", multi: true),
                new AppState { CumulativeTotal = 2, LastDataDate = new DateOnly(2026, 2, 2), StatisticsImported = false });

            Assert.Equal(1, StateStore.LoadOrMigrate(dir.FullName, StateStore.StateFileFor("a", multi: true))!.CumulativeTotal, 6);
            Assert.Equal(2, StateStore.LoadOrMigrate(dir.FullName, StateStore.StateFileFor("b", multi: true))!.CumulativeTotal, 6);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void LegacyFile_NotMigrated_ForPerContractFile()
    {
        var dir = TempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "hariane2mqtt_total_consumption.txt"), "5.0\n01/01/2020 00:00:00");
            Assert.Null(StateStore.LoadOrMigrate(dir.FullName, StateStore.StateFileFor("123", multi: true)));
        }
        finally { dir.Delete(true); }
    }
}
