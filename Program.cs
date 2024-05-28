// See https://aka.ms/new-console-template for more information

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hariane2Mqtt.Hariane;
using HtmlAgilityPack;

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

var apiClient = await new ApiClient(username, password, numContrat, numCompteur).Login(debug);

var lastIndex = await apiClient.GetLastIndex(debug);

DateTime? lastIndexDate = lastIndex?.GetEndDateJour();

var dateFin = lastIndexDate ?? DateTime.Now - TimeSpan.FromDays(1);
var dateDebut = dateFin - TimeSpan.FromDays(ApiClient.maxDays);

Console.WriteLine($"Get data from {dateDebut.Date} to {dateFin.Date}...");

var waterData = await apiClient.GetVisuConso(dateDebut, dateFin, debug);




return 0;