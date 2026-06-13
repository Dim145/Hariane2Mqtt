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
    public DateOnly LastDataDate { get; set; }
    public bool StatisticsImported { get; set; }
}

public static class StateStore
{
    private const string StateFileName = "hariane2mqtt_state.json";
    private const string LegacyFileName = "hariane2mqtt_total_consumption.txt";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Loads the JSON state, or migrates the legacy two-line total file if present
    /// (preserving the cumulative cursor so existing users avoid a fresh heavy backfill).
    /// Returns null when there is no prior state at all (first run ever).
    /// </summary>
    public static AppState? LoadOrMigrate(string directory)
    {
        var path = Path.Combine(directory, StateFileName);
        if (File.Exists(path))
        {
            try
            {
                return JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), JsonOptions);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Could not read state file, treating as first run: {e.Message}");
                return null;
            }
        }

        var legacyPath = Path.Combine(directory, LegacyFileName);
        if (!File.Exists(legacyPath))
            return null;

        try
        {
            var lines = File.ReadAllText(legacyPath).Split('\n');
            var total = double.Parse(lines[0], CultureInfo.InvariantCulture);
            var lastDate = DateTime.Parse(lines[1], CultureInfo.InvariantCulture);

            Console.WriteLine("Migrating legacy total consumption file to state file...");
            return new AppState
            {
                CumulativeTotal = total,
                LastDataDate = DateOnly.FromDateTime(lastDate),
                StatisticsImported = false, // history not yet in HA statistics
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Could not migrate legacy total file, treating as first run: {e.Message}");
            return null;
        }
    }

    public static void Save(string directory, AppState state)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, StateFileName), JsonSerializer.Serialize(state, JsonOptions));
    }
}
