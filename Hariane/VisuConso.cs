using System.Text.Json.Serialization;

namespace Hariane2Mqtt.Hariane;

public class VisuConso
{
    [JsonPropertyName("taillemax")]
    public int Taillemax { get; set; }

    [JsonPropertyName("conso")]
    [JsonConverter(typeof(ConsoListConverter))]
    public List<ConsoPoint> Conso { get; set; } = [];

    [JsonPropertyName("warning")]
    public int[] Warning { get; set; } = [];

    public Dictionary<DateTime, float> GetConso()
    {
        var res = new Dictionary<DateTime, float>();
        foreach (var point in Conso)
            res[point.Date] = point.Value;
        return res;
    }

    public string NumContrat { get; set; } = "";
}
