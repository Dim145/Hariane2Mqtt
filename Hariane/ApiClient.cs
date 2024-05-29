using System.Globalization;
using System.Net;
using System.Text.Json;
using HtmlAgilityPack;

namespace Hariane2Mqtt.Hariane;

public class ApiClient
{
    public const string harianeUrl = "https://www.hariane.fr/";
    public const string harianeApiWaterUrl = $"{harianeUrl}common/getBDDTVisuConso";
    public const string harianeApiGetLastIndex = $"{harianeUrl}common/getBDDTLastIndex";
    public const string harianeApiGetInfosContrat = $"{harianeUrl}common/getInfosContrat";
    public const int maxDays = 17;
    public const string dateFormat = "dd/MM/yyyy";
    
    public static DateTimeFormatInfo FormatInfo { get; } = new()
    {
        ShortDatePattern = "dd/MM/yyyy",
        ShortTimePattern = "HH:mm:ss",
        LongDatePattern = "dd/MM/yyyy HH:mm:ss",
        LongTimePattern = "HH:mm:ss",
    };
    
    private HttpClient Client { get; }
    
    private string Username { get; set; }
    private string Password { get; set; }
    private string NumContrat { get; set; }
    private string NumCompteur { get; set; }
    
    private string CsrfToken { get; set; }
    
    private bool LoggedIn { get; set; }
    
    public ApiClient(string username, string password)
    {
        Username = username;
        Password = password;
        
        Client = new HttpClient();
        Client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
    }

    public ApiClient SetRequiredNums(string numContrat, string numCompteur)
    {
        NumContrat = numContrat;
        NumCompteur = numCompteur;
        
        return this;
    }

    public async Task<ApiClient> Login(bool debug = false)
    {
        Console.WriteLine("Get csrf token...");
        
        var response = await Client.GetAsync(harianeUrl);
        var content = await response.Content.ReadAsStringAsync();

        // get input value from html element with if signin__csrf_token with htmlagilitypack
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);

        var csrftoken = htmlDocument.GetElementbyId("signin__csrf_token")?.GetAttributeValue("value", null);

        if (string.IsNullOrEmpty(csrftoken))
        {
            throw new Exception("Could not find the csrf token.");
        }

        if (debug)
        {
            Console.WriteLine($"csrf token: {csrftoken}");
        }
        
        CsrfToken = csrftoken;
        
        
        // Now we can login
        
        
        Console.WriteLine("Login...");
        
        var loginFormData = new MultipartFormDataContent();

        loginFormData.Add(new StringContent(Username), "signin[username]");
        loginFormData.Add(new StringContent(Password), "signin[password]");
        loginFormData.Add(new StringContent(CsrfToken), "signin[_csrf_token]");

        response = await Client.PostAsync(harianeUrl, loginFormData);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Login failed. (bad response)");
        }

        var responseContent = await response.Content.ReadAsStringAsync();

        if (responseContent.Contains("Votre saisie comporte des erreurs"))
        {
            throw new Exception("Login failed. (bad credentials)");
        }
        
        LoggedIn = true;
        
        return this;
    }
    
    public async Task<LastIndex?> GetLastIndex(bool debug = false)
    {
        if (!LoggedIn)
        {
            throw new Exception("Not logged in.");
        }
        
        Console.WriteLine("Get last index date from hariane...");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "numContrat", NumContrat },
            { "numCompteur", NumCompteur }
        });

        var response = await Client.PostAsync(harianeApiGetLastIndex, formContent);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync("Get last index failed. (bad response)");
            
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<LastIndex>(responseContent);
            
        if (debug)
        {
            Console.WriteLine($"last index date: {json?.LastDateIndex}");
        }
            
        return json;
    }

    public async Task<VisuConso?> GetVisuConso(DateTime startDate, DateTime stopDate, bool debug  = false)
    {
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "numContrat", NumContrat },
            { "numCompteur", NumCompteur },
            { "unite", "m\u00b3" },
            { "typeconso", "jour" },
            { "dateDebut", startDate.ToString(dateFormat) },
            { "dateFin", stopDate.ToString(dateFormat) },
            { "surconso_volume", "" },
            { "surconso_unite", ""}
        });

        var response = await Client.PostAsync(harianeApiWaterUrl, formContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Get data failed. (bad response)");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<VisuConso>(responseContent);

        if (debug)
        {
            Console.WriteLine($"data: {data}");
    
            foreach (var (key, value) in data?.GetConso() ?? new Dictionary<DateTime, float>())
            {
                Console.WriteLine($"{key} => {value}");
            }
        }

        if (data is not null)
        {
            data.NumContrat = NumContrat;
        }
        
        return data;
    }
    
    public async Task<InfosContrat?> GetInfosContrat(string numContrat, bool debug = false)
    {
        if (!LoggedIn)
        {
            throw new Exception("Not logged in.");
        }
        
        Console.WriteLine("Get infos contrat from hariane...");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "id_cnt", numContrat }
        });

        var response = await Client.PostAsync(harianeApiGetInfosContrat, formContent);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync("Get infos contrat failed. (bad response)");
            
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<InfosContrat>(responseContent);
            
        if (debug)
        {
            Console.WriteLine($"infos contrat: {responseContent}");
        }
            
        return json;
    }
}