using System.Globalization;
using System.Text.Json;

namespace Hariane2Mqtt;

/// <summary>
/// Persisted cursor shared by the MQTT total and the Home Assistant statistics import.
/// <see cref="CumulativeTotal"/> is the cumulative m³ through <see cref="LastDataDate"/> (inclusive),
/// kept as a double to avoid the float drift the previous text-file approach suffered from.
/// </summary>
public class AppState
{
    public int SchemaVersion { get; set; } = 1;
    public double CumulativeTotal { get; set; }
    public double CumulativeCost { get; set; }
    public DateOnly LastDataDate { get; set; }
    public bool StatisticsImported { get; set; }
    public bool CostImported { get; set; }

    /// <summary>Recorded flat-price changes (auto mode): each price effective from its date.</summary>
    public List<TariffEntry> Tariffs { get; set; } = new();
}

public static class StateStore
{
    public const string DefaultStateFile = "hariane2mqtt_state.json";
    private const string LegacyFileName = "hariane2mqtt_total_consumption.txt";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// State filename for a contract: the shared default for a single contract (backward-compatible),
    /// or a per-contract suffixed file when several contracts are tracked.
    /// </summary>
    public static string StateFileFor(string slug, bool multi) =>
        multi ? $"hariane2mqtt_state_{slug}.json" : DefaultStateFile;

    /// <summary>
    /// Loads the JSON state, or migrates the legacy two-line total file (single-contract default only),
    /// preserving the cumulative cursor so existing users avoid a fresh heavy backfill.
    /// Returns null when there is no prior state (first run).
    /// </summary>
    public static AppState? LoadOrMigrate(string directory, string stateFileName)
    {
        var path = Path.Combine(directory, stateFileName);
        if (File.Exists(path))
        {
            try
            {
                return JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception e)
            {
                Log.Error($"Could not read state file {stateFileName}, treating as first run: {e.Message}");
                return null;
            }
        }

        // The legacy total file pre-dates multi-contract, so it only seeds the single-contract default.
        if (stateFileName != DefaultStateFile)
            return null;

        var legacyPath = Path.Combine(directory, LegacyFileName);
        if (!File.Exists(legacyPath))
            return null;

        try
        {
            var lines = File.ReadAllText(legacyPath).Split('\n');
            var total = double.Parse(lines[0], CultureInfo.InvariantCulture);
            var lastDate = DateTime.Parse(lines[1], CultureInfo.InvariantCulture);

            Log.Info("Migrating legacy total consumption file to state file...");
            return new AppState
            {
                CumulativeTotal = total,
                LastDataDate = DateOnly.FromDateTime(lastDate),
                StatisticsImported = false, // history not yet in HA statistics
            };
        }
        catch (Exception e)
        {
            Log.Error($"Could not migrate legacy total file, treating as first run: {e.Message}");
            return null;
        }
    }

    public static void Save(string directory, string stateFileName, AppState state)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, stateFileName), JsonSerializer.Serialize(state, JsonOptions));
    }
}
