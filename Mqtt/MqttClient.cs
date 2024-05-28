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

    public async Task Publish(VisuConso conso)
    {
        if (string.IsNullOrEmpty(Topic))
            throw new Exception("Topic is not set.");
        
        if(!Client.IsConnected)
            throw new Exception("Client is not connected.");
        
        var consoDict = conso.GetConso();

        var completeTopic = Path.Combine(Topic, "sensor", $"hariane_{conso.NumContrat}");

        var deviceInfos = new Dictionary<string, string>
        {
            { "identifiers", $"hariane_{conso.NumContrat}" },
            { "manufacturer", "Hariane" },
            { "name", $"Hariane {conso.NumContrat}" },
        };

        var lastIndex = consoDict.ToList().MaxBy(e => e.Key);

        // publish last index state
        await Client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(Path.Combine(completeTopic, "last_index", "state"))
            .WithPayload(JsonSerializer.Serialize(lastIndex.Value))
            .WithRetainFlag()
            .Build());
        
        await Client.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic(Path.Combine(completeTopic, "last_index", "config"))
            .WithPayload(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                {"name", "last_index"},
                {"device_class", "water"},
                {"unit_of_measurement", "m\u00b3"},
                {"state_class", "total"},
                {"device", deviceInfos},
                {"uniq_id", $"hariane_{conso.NumContrat}_last_index"},
                {"state_topic", $"{completeTopic}/last_index/state"}
            }))
            .WithRetainFlag()
            .Build());
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