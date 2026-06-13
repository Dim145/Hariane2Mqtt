using Hariane2Mqtt.Hariane;

namespace Hariane2Mqtt;

public static class Utils
{
    /// <summary>
    /// Walks consumption history backward from <paramref name="endLimit"/> in 17-day batches,
    /// stopping at <paramref name="startLimit"/> or once too many empty ("warning") days appear.
    /// </summary>
    public static async Task<Dictionary<DateTime, float>> GetDataFrom(
        ApiClient client, DateTime startLimit, DateTime endLimit, CancellationToken ct = default)
    {
        var step = TimeSpan.FromDays(ApiClient.maxDays);

        var endDate = endLimit;
        var startDate = endLimit - step < startLimit ? startLimit : endLimit - step;

        var data = new Dictionary<DateTime, float>();
        var warningCount = 0;

        while (startLimit <= startDate && warningCount <= ApiClient.maxDays / 3 * 2)
        {
            Log.Info($"Get data from {startDate.Date:d} to {endDate.Date:d}...");

            var waterData = await client.GetVisuConso(startDate, endDate, ct);
            if (waterData is not null)
            {
                foreach (var (key, value) in waterData.GetConso())
                    data[key] = value;

                warningCount = waterData.Warning.Length;
            }

            endDate = startDate - TimeSpan.FromDays(1);
            startDate = endDate - step;
        }

        return data;
    }
}
