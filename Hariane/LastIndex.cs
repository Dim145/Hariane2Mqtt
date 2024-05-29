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
    
    public DateTime GetEndDateJour()
    {
        return DateTime.Parse(EndDateJour, ApiClient.FormatInfo);
    }
    
    public DateTime GetStartDateJour()
    {
        return DateTime.Parse(StartDateJour, ApiClient.FormatInfo);
    }
}