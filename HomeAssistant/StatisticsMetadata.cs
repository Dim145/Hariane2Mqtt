using System.Text;

namespace Hariane2Mqtt.HomeAssistant;

/// <summary>
/// Metadata for the external Home Assistant statistic that holds the water consumption.
/// The statistic is external (id contains ':'), so it needs no real entity but is still
/// selectable in the Energy dashboard's Water source (because <see cref="UnitClass"/> is "volume").
/// </summary>
public class StatisticsMetadata
{
    /// <summary>Domain prefix of the external statistic. MUST equal the part before ':' in the id.</summary>
    public const string Source = "hariane";

    public const string UnitOfMeasurement = "m³"; // m³

    /// <summary>HA unit converter class for volumes (validated against VolumeConverter).</summary>
    public const string UnitClass = "volume";

    public string StatisticId { get; }
    public string Name { get; }

    public StatisticsMetadata(string numContrat)
    {
        StatisticId = BuildStatisticId(numContrat);
        Name = $"Hariane Water {numContrat}";
    }

    /// <summary>
    /// Builds a valid external statistic id: <c>hariane:water_&lt;slug&gt;</c>.
    /// HA requires lowercase [a-z0-9_], no leading/trailing '_' and no '__'.
    /// </summary>
    public static string BuildStatisticId(string numContrat) => $"{Source}:water_{Slugify(numContrat)}";

    private static string Slugify(string input)
    {
        var sb = new StringBuilder(input.Length);
        var lastWasUnderscore = false;

        foreach (var c in input.ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(c);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore)
            {
                sb.Append('_');
                lastWasUnderscore = true;
            }
        }

        var slug = sb.ToString().Trim('_');
        return slug.Length == 0 ? "meter" : slug;
    }
}
