using System.Globalization;
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

    private const int MaxAttempts = 3;

    public static DateTimeFormatInfo FormatInfo { get; } = new()
    {
        ShortDatePattern = "dd/MM/yyyy",
        ShortTimePattern = "HH:mm:ss",
        LongDatePattern = "dd/MM/yyyy HH:mm:ss",
        LongTimePattern = "HH:mm:ss",
    };

    private HttpClient Client { get; }

    private string Username { get; }
    private string Password { get; }
    private string NumContrat { get; set; } = null!;
    private string NumCompteur { get; set; } = null!;
    private string CsrfToken { get; set; } = null!;

    private bool LoggedIn { get; set; }

    public ApiClient(string username, string password)
    {
        Username = username;
        Password = password;

        Client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        Client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
    }

    public ApiClient SetRequiredNums(string numContrat, string numCompteur)
    {
        NumContrat = numContrat;
        NumCompteur = numCompteur;
        return this;
    }

    public async Task<ApiClient> Login(CancellationToken ct = default)
    {
        Log.Info("Getting CSRF token from hariane.fr...");

        using var landing = await SendWithRetryAsync(() => Client.GetAsync(harianeUrl, ct), "CSRF token request", ct);
        if (!landing.IsSuccessStatusCode)
            throw new HarianeException(HarianeErrorKind.Server, $"Could not load hariane.fr (HTTP {(int)landing.StatusCode}).");

        var content = await landing.Content.ReadAsStringAsync(ct);

        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(content);

        var csrfToken = htmlDocument.GetElementbyId("signin__csrf_token")?.GetAttributeValue("value", "");
        if (string.IsNullOrEmpty(csrfToken))
            throw new HarianeException(HarianeErrorKind.SiteChanged, "CSRF token not found — hariane.fr layout may have changed.");

        Log.Debug($"CSRF token: {csrfToken}");
        CsrfToken = csrfToken;

        Log.Info("Logging in...");

        using var response = await SendWithRetryAsync(
            () => Client.PostAsync(harianeUrl, BuildLoginForm(), ct), "Login", ct);

        if (!response.IsSuccessStatusCode)
            throw new HarianeException(HarianeErrorKind.Server, $"Login request failed (HTTP {(int)response.StatusCode}).");

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        if (responseContent.Contains("Votre saisie comporte des erreurs"))
            throw new HarianeException(HarianeErrorKind.BadCredentials, "Login failed — check HARIANE_USERNAME / HARIANE_PASSWORD.");

        LoggedIn = true;
        Log.Info("Logged in.");
        return this;
    }

    public async Task<LastIndex?> GetLastIndex(CancellationToken ct = default)
    {
        EnsureLoggedIn();
        Log.Info("Getting last index date from hariane.fr...");

        using var response = await SendWithRetryAsync(
            () => Client.PostAsync(harianeApiGetLastIndex, RequiredNumsForm(), ct), "Get last index", ct);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error($"Get last index failed (HTTP {(int)response.StatusCode}).");
            return null;
        }

        var json = JsonSerializer.Deserialize<LastIndex>(await response.Content.ReadAsStringAsync(ct));
        Log.Debug($"Last index date: {json?.LastDateIndex}");
        return json;
    }

    public async Task<VisuConso?> GetVisuConso(DateTime startDate, DateTime stopDate, CancellationToken ct = default)
    {
        EnsureLoggedIn();

        HttpContent Form() => new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "numContrat", NumContrat },
            { "numCompteur", NumCompteur },
            { "unite", "m³" },
            { "typeconso", "jour" },
            { "dateDebut", startDate.ToString(dateFormat) },
            { "dateFin", stopDate.ToString(dateFormat) },
            { "surconso_volume", "" },
            { "surconso_unite", "" },
        });

        using var response = await SendWithRetryAsync(
            () => Client.PostAsync(harianeApiWaterUrl, Form(), ct), "Get consumption", ct);

        if (!response.IsSuccessStatusCode)
            throw new HarianeException(HarianeErrorKind.Server, $"Get consumption failed (HTTP {(int)response.StatusCode}).");

        var data = JsonSerializer.Deserialize<VisuConso>(await response.Content.ReadAsStringAsync(ct));

        if (data is not null)
        {
            data.NumContrat = NumContrat;
            Log.Debug($"Got {data.Conso.Count} day(s), {data.Warning.Length} warning day(s).");
        }

        return data;
    }

    public async Task<InfosContrat?> GetInfosContrat(string numContrat, CancellationToken ct = default)
    {
        EnsureLoggedIn();
        Log.Info("Getting contract info from hariane.fr...");

        using var response = await SendWithRetryAsync(
            () => Client.PostAsync(harianeApiGetInfosContrat,
                new FormUrlEncodedContent(new Dictionary<string, string> { { "id_cnt", numContrat } }), ct),
            "Get contract info", ct);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error($"Get contract info failed (HTTP {(int)response.StatusCode}).");
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        Log.Debug($"Contract info: {body}");
        return JsonSerializer.Deserialize<InfosContrat>(body);
    }

    private MultipartFormDataContent BuildLoginForm() => new()
    {
        { new StringContent(Username), "signin[username]" },
        { new StringContent(Password), "signin[password]" },
        { new StringContent(CsrfToken), "signin[_csrf_token]" },
    };

    private FormUrlEncodedContent RequiredNumsForm() => new(new Dictionary<string, string>
    {
        { "numContrat", NumContrat },
        { "numCompteur", NumCompteur },
    });

    private void EnsureLoggedIn()
    {
        if (!LoggedIn)
            throw new HarianeException(HarianeErrorKind.NotLoggedIn, "Not logged in.");
    }

    /// <summary>
    /// Sends an HTTP request, retrying transient failures (network errors, timeouts, HTTP 5xx)
    /// with exponential backoff. The request content is rebuilt by <paramref name="send"/> on each
    /// attempt (HttpContent is single-use). Non-transient failures surface to the caller.
    /// </summary>
    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send, string action, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await send();
                if ((int)response.StatusCode >= 500 && attempt < MaxAttempts)
                {
                    Log.Warning($"{action}: server returned {(int)response.StatusCode}; retry {attempt}/{MaxAttempts - 1}");
                    response.Dispose();
                    await Task.Delay(Backoff(attempt), ct);
                    continue;
                }
                return response;
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= MaxAttempts)
                    throw new HarianeException(HarianeErrorKind.Network, $"{action} failed after {MaxAttempts} attempts: {e.Message}", e);

                Log.Warning($"{action}: {e.Message}; retry {attempt}/{MaxAttempts - 1}");
                await Task.Delay(Backoff(attempt), ct);
            }
        }
    }

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, ...
}
