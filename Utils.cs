using Hariane2Mqtt.Hariane;

namespace Hariane2Mqtt;

public static class Utils
{
    public static async Task<Dictionary<DateTime, float>> GetDataFrom(ApiClient client, DateTime startLimit, DateTime endLimit, bool debug = false)
    {
        var step = TimeSpan.FromDays(ApiClient.maxDays);
        
        var endDate = endLimit;
        var startDate = endLimit - step < startLimit ? startLimit : endLimit - step;
        
        var data = new Dictionary<DateTime, float>();

        var waterData = new VisuConso();
        
        while (startLimit <= startDate && waterData.Warning.Length <= ApiClient.maxDays / 3 * 2)
        {
            Console.WriteLine($"Get data from {startDate.Date} to {endDate.Date}...");
            
            waterData = await client.GetVisuConso(startDate, endDate, debug);
            
            foreach (var (key, value) in waterData.GetConso())
            {
                data[key] = value;
            }
            
            endDate = startDate - TimeSpan.FromDays(1);
            startDate = endDate - step;
        }
        
        return data;
    }
}