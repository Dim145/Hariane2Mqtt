using System.Globalization;
using System.Text.Json;
using Hariane2Mqtt.Hariane;
using MQTTnet;

namespace Hariane2Mqtt.Mqtt;

public class MqttClient : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly string _topic;
    private readonly string _numContrat;
    private readonly string _node;
    private readonly string _availabilityTopic;

    public MqttClient(string host, string port, string clientId, string topic, string username, string password, string numContrat)
    {
        _topic = topic.TrimEnd('/');
        _numContrat = numContrat;
        _node = $"hariane_{numContrat}";
        _availabilityTopic = $"{_topic}/{_node}/availability";

        _client = new MqttClientFactory().CreateMqttClient();
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, int.Parse(port))
            .WithClientId(clientId)
            .WithCredentials(username, password)
            // Last Will: only fires on an *ungraceful* disconnect (a crash mid-run). A clean exit
            // via DisposeAsync leaves the retained "online" in place — correct for a cron publisher.
            .WithWillTopic(_availabilityTopic)
            .WithWillPayload("offline"u8.ToArray())
            .WithWillRetain()
            .Build();
    }

    public async Task<MqttClient> Connect()
    {
        var result = await _client.ConnectAsync(_options, CancellationToken.None);
        if (result.ResultCode != MqttClientConnectResultCode.Success)
            throw new Exception($"Failed to connect to the broker: {result.ResultCode}");

        await PublishRaw(_availabilityTopic, "online");
        return this;
    }

    public async Task PublishConsumption(VisuConso conso)
    {
        var dict = conso.GetConso();
        if (dict.Count == 0)
        {
            Log.Warning("No consumption points to publish.");
            return;
        }

        var last = dict.MaxBy(e => e.Key);

        // Recent daily history (the fetched window) exposed as attributes of last_value,
        // newest first — handy for templates and cards.
        var history = dict
            .OrderByDescending(e => e.Key)
            .Select(e => new Dictionary<string, object>
            {
                { "date", e.Key.ToString("yyyy-MM-dd") },
                { "value", e.Value },
            })
            .ToList();

        await Publish("sensor", "last_value", Num(last.Value), new Dictionary<string, object>
        {
            { "device_class", "water" },
            { "unit_of_measurement", "m³" },
            { "state_class", "total" },
        }, new Dictionary<string, object>
        {
            { "history", history },
            { "unit_of_measurement", "m³" },
        });

        await Publish("sensor", "last_value_date", last.Key.ToString("yyyy-MM-dd HH:mm:ss"), new Dictionary<string, object>());
    }

    public Task PublishTotal(float total) => Publish("sensor", "total_consomption", Num(total), new Dictionary<string, object>
    {
        { "device_class", "water" },
        { "unit_of_measurement", "m³" },
        { "state_class", "total_increasing" },
    });

    public Task PublishMeterIndex(float index) => Publish("sensor", "index", Num(index), new Dictionary<string, object>
    {
        { "device_class", "water" },
        { "unit_of_measurement", "m³" },
        { "state_class", "total_increasing" },
    });

    public Task PublishLeakSuspected(bool suspected, Dictionary<string, object>? attributes = null) =>
        Publish("binary_sensor", "leak_suspected", suspected ? "ON" : "OFF", new Dictionary<string, object>
        {
            { "device_class", "problem" },
        }, attributes);

    public Task PublishLastUpdate(DateTimeOffset when) =>
        Publish("sensor", "last_update", when.ToString("yyyy-MM-ddTHH:mm:sszzz"), new Dictionary<string, object>
        {
            { "device_class", "timestamp" },
        });

    private Dictionary<string, object> GetDeviceInfos() => new()
    {
        { "identifiers", new[] { _node } },
        { "manufacturer", "Hariane" },
        { "model", "Water meter" },
        { "name", $"Hariane {_numContrat}" },
    };

    private async Task Publish(string component, string name, string state, Dictionary<string, object> config, Dictionary<string, object>? attributes = null)
    {
        if (!_client.IsConnected)
            throw new Exception("MQTT client is not connected.");

        var baseTopic = $"{_topic}/{component}/{_node}/{name}";

        config["name"] = name;
        config["state_topic"] = $"{baseTopic}/state";
        config["unique_id"] = $"{_node}_{name}";
        config["device"] = GetDeviceInfos();
        config["availability_topic"] = _availabilityTopic;
        config["payload_available"] = "online";
        config["payload_not_available"] = "offline";
        if (attributes is not null)
            config["json_attributes_topic"] = $"{baseTopic}/attributes";

        await PublishRaw($"{baseTopic}/state", state);
        await PublishRaw($"{baseTopic}/config", JsonSerializer.Serialize(config));
        if (attributes is not null)
            await PublishRaw($"{baseTopic}/attributes", JsonSerializer.Serialize(attributes));
    }

    private Task PublishRaw(string topic, string payload) =>
        _client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag()
            .Build());

    private static string Num(float value) => value.ToString(CultureInfo.InvariantCulture);

    public Task Disconnect() => _client.DisconnectAsync();

    public async ValueTask DisposeAsync()
    {
        // Clean disconnect — does NOT trigger the Last Will, so the retained "online" persists
        // between cron runs (the published values stay valid).
        if (_client.IsConnected)
            await _client.DisconnectAsync();
        _client.Dispose();
    }
}
