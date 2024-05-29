using System.Globalization;
using System.Text.Json;
using Hariane2Mqtt.Hariane;
using MQTTnet;
using MQTTnet.Client;

namespace Hariane2Mqtt.Mqtt;

public class MqttClient: IAsyncDisposable
{
    private IMqttClient Client { get; }
    
    private MqttClientOptions Options { get; }
    private string? Topic { get; }
    
    public MqttClient(string host, string port, string clientId, string topic, string username, string password)
    {
        var factory = new MqttFactory();

        Client = factory.CreateMqttClient();
        
        Options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, int.Parse(port))
            .WithClientId(clientId)
            .WithCredentials(username, password)
            .Build();
        
        Topic = topic;
    }

    public async Task<MqttClient> Connect()
    {
        var result = await Client.ConnectAsync(Options, CancellationToken.None);
        
        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            throw new Exception($"Failed to connect to the broker: {result.ResultCode}");
        }
        
        return this;
    }

    private Dictionary<string, string> GetDeviceInfos(string numContrat)
    {
        return new Dictionary<string, string>
        {
            { "identifiers", $"hariane_{numContrat}" },
            { "manufacturer", "Hariane" },
            { "name", $"Hariane {numContrat}" },
        };
    }

    public async Task Publish(VisuConso conso)
    {
        if (string.IsNullOrEmpty(Topic))
            throw new Exception("Topic is not set.");
        
        if(!Client.IsConnected)
            throw new Exception("Client is not connected.");
        
        var consoDict = conso.GetConso();

        var completeTopic = Path.Combine(Topic, "sensor", $"hariane_{conso.NumContrat}");

        var deviceInfos = GetDeviceInfos(conso.NumContrat);

        var lastValue = consoDict.ToList().MaxBy(e => e.Key);
        

        await Publish(completeTopic, "last_value", lastValue.Value, new Dictionary<string, object>
        {
            {"device", deviceInfos},
            { "device_class", "water" },
            { "unit_of_measurement", "m\u00b3" },
            { "state_class", "total" },
        });
        await Publish(completeTopic, "last_value_date", lastValue.Key.ToString("yyyy-MM-dd HH:mm:ss"), new Dictionary<string, object>
        {
            {"device", deviceInfos},
        });
    }

    public async Task PublishTotalConsuption(string numContrat, float total)
    {
        var completeTopic = Path.Combine(Topic, "sensor", $"hariane_{numContrat}");
        
        await Publish(completeTopic, "total_consomption", total, new Dictionary<string, object>
        {
            {"device", GetDeviceInfos(numContrat)},
            { "device_class", "water" },
            { "unit_of_measurement", "m\u00b3" },
            { "state_class", "total_increasing" },
        });
    }

    private async Task Publish<T>(string completeTopic, string name, T value, Dictionary<string, object> config, Dictionary<string, object>? attributes = null)
    {
        config["name"] = name;
        config["state_topic"] = $"{completeTopic}/{name}/state";
        config["uniq_id"] = $"{(config["device"] as Dictionary<string, string>)?["identifiers"] ?? completeTopic.Replace("/", "_")}_{name}";
        
        await Client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(Path.Combine(completeTopic, name, "state"))
            .WithPayload(value?.GetType() == typeof(string) ? value.ToString() : JsonSerializer.Serialize(value))
            .WithRetainFlag()
            .Build());
        
        await Client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(Path.Combine(completeTopic, name, "config"))
            .WithPayload(JsonSerializer.Serialize(config))
            .WithRetainFlag()
            .Build());

        if (attributes != null)
        {
            await Client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(Path.Combine(completeTopic, name, "attributes"))
                .WithPayload(JsonSerializer.Serialize(attributes))
                .WithRetainFlag()
                .Build());
        }
    }
    
    public async Task Disconnect()
    {
        await Client.DisconnectAsync();
    }
    
    public async ValueTask DisposeAsync()
    {
        await Disconnect();
        Client.Dispose();
    }
}