namespace Hariane2Mqtt.HomeAssistant;

/// <summary>
/// Resolves how to reach the Home Assistant WebSocket API and which token to use.
/// Add-on mode is detected by the presence of <c>SUPERVISOR_TOKEN</c> (injected by the
/// supervisor); otherwise standalone mode requires <c>HASS_URL</c> + <c>HASS_TOKEN</c>.
/// </summary>
public static class HassConnection
{
    public static (Uri Uri, string Token) Resolve()
    {
        var supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
        if (!string.IsNullOrEmpty(supervisorToken))
        {
            // Add-on: the supervisor proxies the core websocket.
            return (new Uri("ws://supervisor/core/websocket"), supervisorToken);
        }

        var hassUrl = Environment.GetEnvironmentVariable("HASS_URL");
        var hassToken = Environment.GetEnvironmentVariable("HASS_TOKEN");

        if (string.IsNullOrEmpty(hassUrl) || string.IsNullOrEmpty(hassToken))
        {
            throw new Exception(
                "IMPORT_ENERGY_STATISTICS is enabled but no Home Assistant connection is available. " +
                "Set HASS_URL and HASS_TOKEN (standalone), or run as a Home Assistant add-on (SUPERVISOR_TOKEN).");
        }

        var baseUri = new Uri(hassUrl);
        var scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        var wsUri = new UriBuilder(baseUri) { Scheme = scheme, Path = "/api/websocket", Query = string.Empty }.Uri;

        return (wsUri, hassToken);
    }
}
