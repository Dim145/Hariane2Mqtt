using System.Globalization;
using System.Runtime.InteropServices;
using Hariane2Mqtt;
using Hariane2Mqtt.Hariane;
using Hariane2Mqtt.HomeAssistant;
using Hariane2Mqtt.Mqtt;

// --- configuration (environment variables) ---

var debug = bool.Parse(Environment.GetEnvironmentVariable("DEBUG") ?? "false");
Log.Configure(Environment.GetEnvironmentVariable("LOG_LEVEL"), debug);

var calculateTotalConsumption = bool.Parse(Environment.GetEnvironmentVariable("CALCULATE_TOTAL_CONSUMPTION") ?? "false");
var importStats = bool.Parse(Environment.GetEnvironmentVariable("IMPORT_ENERGY_STATISTICS") ?? "false");
var leakDetection = bool.Parse(Environment.GetEnvironmentVariable("LEAK_DETECTION") ?? "false");
var leakMinDays = int.Parse(Environment.GetEnvironmentVariable("LEAK_MIN_DAYS") ?? "3");
var leakThreshold = float.Parse(Environment.GetEnvironmentVariable("LEAK_DAILY_THRESHOLD") ?? "0.1", CultureInfo.InvariantCulture);
var directoryForData = Environment.GetEnvironmentVariable("DIRECTORY_FOR_DATA") ?? "/data";

var username = Environment.GetEnvironmentVariable("HARIANE_USERNAME");
var password = Environment.GetEnvironmentVariable("HARIANE_PASSWORD");

if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
{
    Log.Fatal("Please set the HARIANE_USERNAME and HARIANE_PASSWORD environment variables.");
    return 1;
}

var numContrat = Environment.GetEnvironmentVariable("HARIANE_NUM_CONTRAT");

if (string.IsNullOrEmpty(numContrat))
{
    Log.Fatal("Please set the HARIANE_NUM_CONTRAT environment variable.");
    return 1;
}

var mqttBroker = Environment.GetEnvironmentVariable("MQTT_HOST");
var mqttPort = Environment.GetEnvironmentVariable("MQTT_PORT") ?? "1883";
var mqttClientId = Environment.GetEnvironmentVariable("MQTT_CLIENT_ID");
var mqttUsername = Environment.GetEnvironmentVariable("MQTT_USERNAME");
var mqttPassword = Environment.GetEnvironmentVariable("MQTT_PASSWORD");
var mqttTopic = Environment.GetEnvironmentVariable("MQTT_TOPIC");

if (string.IsNullOrEmpty(mqttBroker) || string.IsNullOrEmpty(mqttClientId) || string.IsNullOrEmpty(mqttUsername) || string.IsNullOrEmpty(mqttPassword) || string.IsNullOrEmpty(mqttTopic))
{
    Log.Fatal("Please set the MQTT_HOST, MQTT_CLIENT_ID, MQTT_USERNAME, MQTT_PASSWORD and MQTT_TOPIC environment variables.");
    return 1;
}

// graceful shutdown on Ctrl+C (SIGINT) and `docker stop` (SIGTERM)
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; cts.Cancel(); });
var ct = cts.Token;

try
{
    var apiClient = await new ApiClient(username, password).Login(ct);

    var infosContrat = await apiClient.GetInfosContrat(numContrat, ct);
    var numCompteur = infosContrat?.M2ONumCpt;

    if (string.IsNullOrWhiteSpace(numCompteur))
    {
        Log.Fatal("Could not get the meter number.");
        return 1;
    }

    var lastIndex = await apiClient.SetRequiredNums(numContrat, numCompteur).GetLastIndex(ct);

    var dateFin = lastIndex?.GetEndDateJour() ?? DateTime.Now - TimeSpan.FromDays(1);
    var dateDebut = dateFin - TimeSpan.FromDays(ApiClient.maxDays);

    Log.Info($"Get data from {dateDebut.Date:d} to {dateFin.Date:d}...");

    var waterData = await apiClient.GetVisuConso(dateDebut, dateFin, ct);

    await using var mqttClient = new MqttClient(mqttBroker, mqttPort, mqttClientId, mqttTopic, mqttUsername, mqttPassword, numContrat);
    await mqttClient.Connect();

    if (waterData is not null)
    {
        await mqttClient.PublishConsumption(waterData);

        if (leakDetection)
        {
            var (suspected, minDaily, days) = LeakDetector.Evaluate(waterData.GetConso(), leakMinDays, leakThreshold);
            await mqttClient.PublishLeakSuspected(suspected, new Dictionary<string, object>
            {
                { "min_daily_m3", minDaily },
                { "days_considered", days },
                { "threshold_m3", leakThreshold },
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

    // --- Cumulative total (MQTT) and/or Energy statistics import (Home Assistant) ---

    if (calculateTotalConsumption || importStats)
    {
        var state = StateStore.LoadOrMigrate(directoryForData);
        var (fullHistory, from) = ConsumptionCursor.PlanWindow(state, importStats);

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

        // 1. MQTT total sensor (now backed by a double cumulative).
        if (calculateTotalConsumption)
        {
            await mqttClient.PublishTotal((float)newCumulative);
            Log.Info($"Total consumption: {newCumulative} m3");
        }

        // 2. Energy statistics import, dated to the correct day (Home Assistant Energy dashboard).
        var statisticsImported = state?.StatisticsImported ?? false;

        if (importStats && series.Count > 0)
        {
            var (wsUri, token) = HassConnection.Resolve();
            await using var hass = new HassWebSocketClient(wsUri, token);

            using var wsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            wsCts.CancelAfter(TimeSpan.FromSeconds(60));

            await hass.ConnectAndAuthAsync(wsCts.Token);
            var tz = TimeZoneInfo.FindSystemTimeZoneById(await hass.GetTimeZoneAsync(wsCts.Token));

            var (points, _, _) = StatisticsBuilder.Build(series, baseCumulative, tz);
            await hass.ImportStatisticsAsync(new StatisticsMetadata(numContrat), points, wsCts.Token);

            statisticsImported = true;
        }

        // 3. Persist the cursor only once MQTT + import have succeeded.
        if (newLastDay.HasValue)
        {
            StateStore.Save(directoryForData, new AppState
            {
                CumulativeTotal = newCumulative,
                LastDataDate = newLastDay.Value,
                StatisticsImported = statisticsImported,
            });
        }
    }

    await mqttClient.PublishLastUpdate(DateTimeOffset.Now);

    return 0;
}
catch (HarianeException e)
{
    Log.Fatal($"Hariane error [{e.Kind}]: {e.Message}");
    return 1;
}
catch (OperationCanceledException)
{
    Log.Warning("Run cancelled.");
    return 1;
}
catch (Exception e)
{
    Log.Fatal($"Unexpected error: {e.Message}");
    Log.Debug(e.ToString());
    return 1;
}
