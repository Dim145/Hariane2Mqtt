using System.Globalization;
using Hariane2Mqtt.Hariane;
using Hariane2Mqtt.Mqtt;

// get and check environment variables

var debug = bool.Parse(Environment.GetEnvironmentVariable("DEBUG") ?? "false");
var calculateTotalComsumption = bool.Parse(Environment.GetEnvironmentVariable("CALCULATE_TOTAL_CONSUMPTION") ?? "false");

var username = Environment.GetEnvironmentVariable("HARIANE_USERNAME");
var password = Environment.GetEnvironmentVariable("HARIANE_PASSWORD");

if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine("Please set the HARIANE_USERNAME and HARIANE_PASSWORD environment variables.");
    
    return 1;
}

var numContrat = Environment.GetEnvironmentVariable("HARIANE_NUM_CONTRAT");

if (string.IsNullOrEmpty(numContrat))
{
    Console.Error.WriteLine("Please set the HARIANE_NUM_CONTRAT environment variable.");
    
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


var apiClient = await new ApiClient(username, password).Login(debug);

var infosContrat = await apiClient.GetInfosContrat(numContrat, debug);
var numCompteur = infosContrat?.M2ONumCpt;

if (string.IsNullOrWhiteSpace(numCompteur))
{
    Console.Error.WriteLine("Could not get the meter number.");
    
    return 1;
}

var lastIndex = await apiClient.SetRequiredNums(numContrat, numCompteur).GetLastIndex(debug);

DateTime? lastIndexDate = lastIndex?.GetEndDateJour();

var dateFin = lastIndexDate ?? DateTime.Now - TimeSpan.FromDays(1);
var dateDebut = dateFin - TimeSpan.FromDays(ApiClient.maxDays);

Console.WriteLine($"Get data from {dateDebut.Date} to {dateFin.Date}...");

var waterData = await apiClient.GetVisuConso(dateDebut, dateFin, debug);

var mqttClient = new MqttClient(mqttBroker, mqttPort, mqttClientId, mqttTopic, mqttUsername, mqttPassword);

await mqttClient.Connect();

await mqttClient.Publish(waterData);

var fileName = Path.Combine("/data", "hariane2mqtt_total_consumption.txt");

if (calculateTotalComsumption)
{
    var totalConsumption = 0f;
    
    if (File.Exists(fileName))
    {
        Console.WriteLine("Read total consumption from file...");
        
        totalConsumption = float.Parse(await File.ReadAllTextAsync(fileName), CultureInfo.InvariantCulture);

        totalConsumption += waterData.GetConso().ToList().MaxBy(e => e.Key).Value;
        
        await File.WriteAllTextAsync(fileName, totalConsumption.ToString(CultureInfo.InvariantCulture));
    
        await mqttClient.PublishTotalConsuption(numContrat, totalConsumption);
    }
    else
    {
        Console.WriteLine("Calculate total consumption...");
        
        var allWaterData = waterData.GetConso();

        do
        {
            var tmp = dateDebut;
            dateDebut -= TimeSpan.FromDays(ApiClient.maxDays + 1);
            dateFin = tmp - TimeSpan.FromDays(1);

            Console.WriteLine($"Get data from {dateDebut.Date} to {dateFin.Date}...");

            waterData = await apiClient.GetVisuConso(dateDebut, dateFin, debug);

            allWaterData = allWaterData.Concat(waterData.GetConso()).ToDictionary(x => x.Key, x => x.Value);
        } 
        while (waterData.Warning.Length <= ApiClient.maxDays / 3 * 2); // permit days off
    
        totalConsumption = allWaterData.Values.Sum();
        
        await mqttClient.PublishTotalConsuption(numContrat, totalConsumption);  
        
        if (!Directory.Exists(Path.GetDirectoryName(fileName)))
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
    
        await File.WriteAllTextAsync(fileName, totalConsumption.ToString(CultureInfo.InvariantCulture));
    }
    
    Console.WriteLine($"Total consumption: {totalConsumption} m3");
}

return 0;