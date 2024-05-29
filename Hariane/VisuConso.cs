using System.Globalization;
using System.Text.Json.Serialization;

namespace Hariane2Mqtt.Hariane;

public class VisuConso
{
    [JsonPropertyName("taillemax")]
    public  int Taillemax { get; set; }
    
    [JsonPropertyName("conso")]
    public  List<object[]> Conso { get; set; }

    public Dictionary<DateTime, float> GetConso()
    {
        var res = new Dictionary<DateTime, float>();

        foreach (var line in Conso)
        {
            try
            {
                var dateStr = line[0]?.ToString();
                var valueStr = line[1]?.ToString();

                res.Add(DateTime.Parse(dateStr.Split(" ").Last(), ApiClient.FormatInfo), float.Parse(valueStr, CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                // log exception but continue
                Console.Error.WriteLine(e);
            }
        }

        return res;
    }
    
    public string NumContrat { get; set; } = "";
}