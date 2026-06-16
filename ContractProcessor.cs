using Hariane2Mqtt.Hariane;
using Hariane2Mqtt.HomeAssistant;
using Hariane2Mqtt.Mqtt;

namespace Hariane2Mqtt;

/// <summary>Runs the full per-contract flow: fetch → MQTT publish → (optional) total + Energy statistics.</summary>
public static class ContractProcessor
{
    public static async Task ProcessAsync(ApiClient apiClient, string numContrat, bool multi, RunConfig cfg, CancellationToken ct)
    {
        Log.Info($"=== Contract {numContrat} ===");

        var infosContrat = await apiClient.GetInfosContrat(numContrat, ct);
        var numCompteur = infosContrat?.M2ONumCpt;

        if (string.IsNullOrWhiteSpace(numCompteur))
        {
            Log.Error($"Could not get the meter number for contract {numContrat}; skipping.");
            return;
        }

        var lastIndex = await apiClient.SetRequiredNums(numContrat, numCompteur).GetLastIndex(ct);

        var dateFin = lastIndex?.GetEndDateJour() ?? DateTime.Now - TimeSpan.FromDays(1);
        var dateDebut = dateFin - TimeSpan.FromDays(ApiClient.maxDays);

        Log.Info($"Get data from {dateDebut.Date:d} to {dateFin.Date:d}...");
        var waterData = await apiClient.GetVisuConso(dateDebut, dateFin, ct);

        var slug = StatisticsMetadata.Slug(numContrat);
        var clientId = multi ? $"{cfg.MqttClientId}_{slug}" : cfg.MqttClientId;

        await using var mqttClient = new MqttClient(
            cfg.MqttHost, cfg.MqttPort, clientId, cfg.MqttTopic, cfg.MqttUsername, cfg.MqttPassword, numContrat);
        await mqttClient.Connect();

        if (waterData is not null)
        {
            await mqttClient.PublishConsumption(waterData);

            if (cfg.LeakDetection)
            {
                var (suspected, minDaily, days) = LeakDetector.Evaluate(waterData.GetConso(), cfg.LeakMinDays, cfg.LeakThreshold);
                await mqttClient.PublishLeakSuspected(suspected, new Dictionary<string, object>
                {
                    { "min_daily_m3", minDaily },
                    { "days_considered", days },
                    { "threshold_m3", cfg.LeakThreshold },
                });
                Log.Info($"Leak suspected: {suspected} (min daily {minDaily} m³ over {days} day(s)).");
            }
        }
        else
        {
            Log.Warning("No current consumption data returned; skipping last-value publish.");
        }

        if (lastIndex is not null)
            await mqttClient.PublishMeterIndex(lastIndex.Index);

        if (cfg.CalculateTotal || cfg.ImportStats)
            await ProcessTotalsAndStatistics(apiClient, numContrat, slug, multi, cfg, mqttClient, dateFin, ct);

        await mqttClient.PublishLastUpdate(DateTimeOffset.Now);
    }

    private static async Task ProcessTotalsAndStatistics(
        ApiClient apiClient, string numContrat, string slug, bool multi, RunConfig cfg, MqttClient mqttClient, DateTime dateFin, CancellationToken ct)
    {
        var stateFile = StateStore.StateFileFor(slug, multi);
        var state = StateStore.LoadOrMigrate(cfg.DirectoryForData, stateFile);
        var (fullHistory, from) = ConsumptionCursor.PlanWindow(state, cfg.ImportStats, costEnabled: !cfg.Tariff.IsEmpty);

        Dictionary<DateTime, float> series;
        double baseCumulative;

        if (fullHistory)
        {
            Log.Info("Calculating full consumption history (one-time, can take a while)...");
            series = await Utils.GetDataFrom(apiClient, DateTime.MinValue, dateFin, ct);
            baseCumulative = 0d;
        }
        else
        {
            baseCumulative = state!.CumulativeTotal;
            if (from.Date <= dateFin.Date)
            {
                Log.Info($"Fetching new consumption from {from.Date:d} to {dateFin.Date:d}...");
                series = await Utils.GetDataFrom(apiClient, from, dateFin, ct);
            }
            else
            {
                Log.Info("No new consumption data since last run.");
                series = new Dictionary<DateTime, float>();
            }
        }

        var (newCumulative, newLastDay) = ConsumptionCursor.Advance(baseCumulative, series, state);

        if (cfg.CalculateTotal)
        {
            await mqttClient.PublishTotal((float)newCumulative);
            Log.Info($"Total consumption: {newCumulative} m3");
        }

        var statisticsImported = state?.StatisticsImported ?? false;
        var newCost = state?.CumulativeCost ?? 0d;
        var costImported = state?.CostImported ?? false;

        if (cfg.ImportStats && series.Count > 0)
        {
            var (wsUri, token) = HassConnection.Resolve();
            await using var hass = new HassWebSocketClient(wsUri, token);

            using var wsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wsCts.CancelAfter(TimeSpan.FromSeconds(60));

            await hass.ConnectAndAuthAsync(wsCts.Token);

            var config = await hass.GetConfigAsync(wsCts.Token);
            var tzId = config?["time_zone"]?.GetValue<string>()
                       ?? throw new Exception("Could not read time_zone from Home Assistant.");
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);

            var (points, _, _) = StatisticsBuilder.Build(series, baseCumulative, tz);
            await hass.ImportStatisticsAsync(StatisticsMetadata.Water(numContrat), points, wsCts.Token);

            if (!cfg.Tariff.IsEmpty)
            {
                var currency = config?["currency"]?.GetValue<string>() ?? "EUR";
                var baseCost = fullHistory ? 0d : state?.CumulativeCost ?? 0d;

                // Cost of each day = that day's consumption × the price in effect on that day.
                var costSeries = series.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value * cfg.Tariff.PriceOn(DateOnly.FromDateTime(kv.Key)));

                var (costPoints, costCumulative, _) = StatisticsBuilder.Build(costSeries, baseCost, tz);
                await hass.ImportStatisticsAsync(StatisticsMetadata.WaterCost(numContrat, currency), costPoints, wsCts.Token);

                newCost = costCumulative;
                costImported = true;
                Log.Info($"Imported water cost statistics ({currency}).");
            }

            statisticsImported = true;
        }

        if (newLastDay.HasValue)
        {
            StateStore.Save(cfg.DirectoryForData, stateFile, new AppState
            {
                CumulativeTotal = newCumulative,
                CumulativeCost = newCost,
                LastDataDate = newLastDay.Value,
                StatisticsImported = statisticsImported,
                CostImported = costImported,
            });
        }
    }
}
