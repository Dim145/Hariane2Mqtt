using Hariane2Mqtt.Hariane;
using Hariane2Mqtt.Mqtt;

// get and check environment variables

var debug = bool.Parse(Environment.GetEnvironmentVariable("DEBUG") ?? "false");

var username = Environment.GetEnvironmentVariable("HARIANE_USERNAME");
var password = Environment.GetEnvironmentVariable("HARIANE_PASSWORD");

if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine("Please set the HARIANE_USERNAME and HARIANE_PASSWORD environment variables.");
    
    return 1;
}

var numContrat = Environment.GetEnvironmentVariable("HARIANE_NUM_CONTRAT");
var numCompteur = Environment.GetEnvironmentVariable("HARIANE_NUM_COMPTEUR");

if (string.IsNullOrEmpty(numContrat) || string.IsNullOrEmpty(numCompteur))
{
    Console.Error.WriteLine("Please set the HARIANE_NUM_CONTRAT and HARIANE_NUM_COMPTEUR environment variables.");
    
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
    Console.Error.WriteLine("Please set the MQTT_BROKER, MQTT_CLIENT_ID, MQTT_USERNAME, MQTT_PASSWORD and MQTT_TOPIC environment variables.");
    
    return 1;
}

// start script


var apiClient = await new ApiClient(username, password, numContrat, numCompteur).Login(debug);

var lastIndex = await apiClient.GetLastIndex(debug);

DateTime? lastIndexDate = lastIndex?.GetEndDateJour();

var dateFin = lastIndexDate ?? DateTime.Now - TimeSpan.FromDays(1);
var dateDebut = dateFin - TimeSpan.FromDays(ApiClient.maxDays);

Console.WriteLine($"Get data from {dateDebut.Date} to {dateFin.Date}...");

var waterData = await apiClient.GetVisuConso(dateDebut, dateFin, debug);

var mqttClient = new MqttClient(mqttBroker, mqttPort, mqttClientId, mqttTopic, mqttUsername, mqttPassword);

await mqttClient.Connect();

await mqttClient.Publish(waterData);

return 0;