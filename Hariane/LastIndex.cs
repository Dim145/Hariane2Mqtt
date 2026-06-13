using System.Globalization;
using System.Text.Json.Serialization;

namespace Hariane2Mqtt.Hariane;

public class LastIndex
{
    [JsonPropertyName("enddatejour")]
    public string? EndDateJour { get; set; }

    [JsonPropertyName("startdatejour")]
    public string? StartDateJour { get; set; }

    [JsonPropertyName("index")]
    public float Index { get; set; }

    [JsonPropertyName("lastdateindex")]
    public string LastDateIndex { get; set; } = "";

    public DateTime? GetEndDateJour() => Parse(EndDateJour);

    public DateTime? GetStartDateJour() => Parse(StartDateJour);

    private static DateTime? Parse(string? value) =>
        DateTime.TryParse(value, ApiClient.FormatInfo, DateTimeStyles.None, out var d) ? d : null;
}
