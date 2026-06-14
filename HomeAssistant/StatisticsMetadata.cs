using System.Text;

namespace Hariane2Mqtt.HomeAssistant;

/// <summary>
/// Metadata for an external Home Assistant statistic — water consumption (m³) or its cost
/// (in the HA currency). External statistics need no real entity but are selectable in the
/// Energy dashboard's Water source.
/// </summary>
public class StatisticsMetadata
{
    /// <summary>Domain prefix of the external statistic. MUST equal the part before ':' in the id.</summary>
    public const string Source = "hariane";

    public string StatisticId { get; }
    public string Name { get; }
    public string UnitOfMeasurement { get; }

    /// <summary>HA unit-converter class ("volume" for m³), or null when there is no converter (cost).</summary>
    public string? UnitClass { get; }

    private StatisticsMetadata(string statisticId, string name, string unitOfMeasurement, string? unitClass)
    {
        StatisticId = statisticId;
        Name = name;
        UnitOfMeasurement = unitOfMeasurement;
        UnitClass = unitClass;
    }

    /// <summary>Water consumption in m³ (unit_class "volume") — for Energy → Water consumption.</summary>
    public static StatisticsMetadata Water(string numContrat) =>
        new(BuildStatisticId(numContrat), $"Hariane Water {numContrat}", "m³", "volume");

    /// <summary>Cumulative water cost in the HA currency — for Energy → Water cost.</summary>
    public static StatisticsMetadata WaterCost(string numContrat, string currency) =>
        new($"{Source}:water_cost_{Slugify(numContrat)}", $"Hariane Water Cost {numContrat}", currency, null);

    /// <summary>Builds the water statistic id: <c>hariane:water_&lt;slug&gt;</c>.</summary>
    public static string BuildStatisticId(string numContrat) => $"{Source}:water_{Slugify(numContrat)}";

    /// <summary>Lowercase slug of a contract number, reused for state filenames and MQTT client ids.</summary>
    public static string Slug(string numContrat) => Slugify(numContrat);

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
