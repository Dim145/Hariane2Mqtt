using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hariane2Mqtt.Hariane;

/// <summary>One day of consumption: a date and its volume (m³).</summary>
public readonly record struct ConsoPoint(DateTime Date, float Value);

/// <summary>
/// Parses Hariane's loosely-typed <c>"conso"</c> payload — an array of <c>[dateLabel, value]</c>
/// pairs — into strongly-typed <see cref="ConsoPoint"/>s. Malformed rows (headers, nulls) are
/// skipped rather than failing the whole response.
/// </summary>
public class ConsoListConverter : JsonConverter<List<ConsoPoint>>
{
    public override List<ConsoPoint> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<ConsoPoint>();
        using var doc = JsonDocument.ParseValue(ref reader);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 2)
                continue;

            try
            {
                var cells = row.EnumerateArray().ToArray();
                var dateLabel = cells[0].ToString();                       // e.g. "lun. 10/06/2026"
                var valueText = cells[1].ValueKind == JsonValueKind.String
                    ? cells[1].GetString()
                    : cells[1].GetRawText();

                if (string.IsNullOrWhiteSpace(dateLabel) || string.IsNullOrWhiteSpace(valueText))
                    continue;

                var date = DateTime.Parse(dateLabel.Split(' ').Last(), ApiClient.FormatInfo);
                var value = float.Parse(valueText, CultureInfo.InvariantCulture);
                list.Add(new ConsoPoint(date, value));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Skipping malformed conso row: {e.Message}");
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<ConsoPoint> value, JsonSerializerOptions options)
        => throw new NotSupportedException("VisuConso is read-only.");
}
