using System.Globalization;
using System.Runtime.InteropServices;
using Hariane2Mqtt;
using Hariane2Mqtt.Hariane;

// --- configuration (environment variables) ---

var debug = bool.Parse(Environment.GetEnvironmentVariable("DEBUG") ?? "false");
Log.Configure(Environment.GetEnvironmentVariable("LOG_LEVEL"), debug);

var username = Environment.GetEnvironmentVariable("HARIANE_USERNAME");
var password = Environment.GetEnvironmentVariable("HARIANE_PASSWORD");

if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
{
    Log.Fatal("Please set the HARIANE_USERNAME and HARIANE_PASSWORD environment variables.");
    return 1;
}

var numContratRaw = Environment.GetEnvironmentVariable("HARIANE_NUM_CONTRAT");

if (string.IsNullOrEmpty(numContratRaw))
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

TariffSchedule tariff;
try
{
    tariff = TariffSchedule.Parse(Environment.GetEnvironmentVariable("PRICE_PER_M3"));
}
catch (Exception e)
{
    Log.Fatal($"Invalid PRICE_PER_M3 (expected a number or 'YYYY-MM-DD:price,…'): {e.Message}");
    return 1;
}

var config = new RunConfig
{
    MqttHost = mqttBroker,
    MqttPort = mqttPort,
    MqttClientId = mqttClientId,
    MqttUsername = mqttUsername,
    MqttPassword = mqttPassword,
    MqttTopic = mqttTopic,
    CalculateTotal = bool.Parse(Environment.GetEnvironmentVariable("CALCULATE_TOTAL_CONSUMPTION") ?? "false"),
    ImportStats = bool.Parse(Environment.GetEnvironmentVariable("IMPORT_ENERGY_STATISTICS") ?? "false"),
    Tariff = tariff,
    LeakDetection = bool.Parse(Environment.GetEnvironmentVariable("LEAK_DETECTION") ?? "false"),
    LeakMinDays = int.Parse(Environment.GetEnvironmentVariable("LEAK_MIN_DAYS") ?? "3"),
    LeakThreshold = float.Parse(Environment.GetEnvironmentVariable("LEAK_DAILY_THRESHOLD") ?? "0.1", CultureInfo.InvariantCulture),
    DirectoryForData = Environment.GetEnvironmentVariable("DIRECTORY_FOR_DATA") ?? "/data",
};

// HARIANE_NUM_CONTRAT may list several contracts, comma-separated.
var contracts = numContratRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var multi = contracts.Length > 1;

if (contracts.Length == 0)
{
    Log.Fatal("HARIANE_NUM_CONTRAT does not contain a valid contract number.");
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

    var hadError = false;
    foreach (var contrat in contracts)
    {
        try
        {
            await ContractProcessor.ProcessAsync(apiClient, contrat, multi, config, ct);
        }
        catch (OperationCanceledException)
        {
            throw; // a cancellation is global — stop the whole run
        }
        catch (HarianeException e)
        {
            Log.Error($"Contract {contrat}: Hariane error [{e.Kind}]: {e.Message}");
            hadError = true;
        }
        catch (Exception e)
        {
            Log.Error($"Contract {contrat} failed: {e.Message}");
            Log.Debug(e.ToString());
            hadError = true;
        }
    }

    return hadError ? 1 : 0;
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
