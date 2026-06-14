using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Hariane2Mqtt.HomeAssistant;

/// <summary>
/// Minimal Home Assistant WebSocket API client: performs the auth handshake, reads the
/// instance timezone, and imports long-term statistics via <c>recorder/import_statistics</c>.
/// Commands are issued strictly sequentially, so a simple "send then read until our id" loop
/// is enough (no concurrent request map needed). Frame-level traces are emitted at Debug level.
/// </summary>
public class HassWebSocketClient : IAsyncDisposable
{
    private readonly Uri _uri;
    private readonly string _token;
    private readonly ClientWebSocket _ws = new();
    private int _id;

    public HassWebSocketClient(Uri uri, string token)
    {
        _uri = uri;
        _token = token;
    }

    public async Task ConnectAndAuthAsync(CancellationToken ct = default)
    {
        Log.Info($"Connecting to Home Assistant websocket {_uri}...");
        await _ws.ConnectAsync(_uri, ct);

        var authRequired = await ReceiveJsonAsync(ct);
        if (authRequired?["type"]?.GetValue<string>() != "auth_required")
            throw new Exception($"Unexpected first message from Home Assistant: {authRequired?["type"]}");

        await SendRawAsync(new JsonObject { ["type"] = "auth", ["access_token"] = _token }, ct);

        var authResult = await ReceiveJsonAsync(ct);
        var authType = authResult?["type"]?.GetValue<string>();
        if (authType != "auth_ok")
        {
            var msg = authResult?["message"]?.GetValue<string>() ?? "unknown reason";
            throw new Exception($"Home Assistant authentication failed: {msg}");
        }

        Log.Info("Authenticated with Home Assistant.");
    }

    /// <summary>Returns the Home Assistant <c>get_config</c> result (time_zone, currency, …).</summary>
    public Task<JsonNode?> GetConfigAsync(CancellationToken ct = default) =>
        SendCommandAsync(new JsonObject { ["type"] = "get_config" }, ct);

    public async Task ImportStatisticsAsync(
        StatisticsMetadata metadata, IReadOnlyList<HassStatePoint> points, CancellationToken ct = default)
    {
        var stats = new JsonArray();
        foreach (var p in points)
        {
            var entry = new JsonObject
            {
                ["start"] = p.Start.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                ["sum"] = p.Sum,
            };
            if (p.State.HasValue)
                entry["state"] = p.State.Value;
            stats.Add(entry);
        }

        var command = new JsonObject
        {
            ["type"] = "recorder/import_statistics",
            ["metadata"] = new JsonObject
            {
                ["has_sum"] = true,
                ["mean_type"] = 0, // 0 = none (replaces the deprecated has_mean bool, removed in HA 2026.11)
                ["name"] = metadata.Name,
                ["source"] = StatisticsMetadata.Source,
                ["statistic_id"] = metadata.StatisticId,
                ["unit_class"] = metadata.UnitClass,
                ["unit_of_measurement"] = metadata.UnitOfMeasurement,
            },
            ["stats"] = stats,
        };

        await SendCommandAsync(command, ct);
        Log.Info($"Imported {points.Count} statistics point(s) into '{metadata.StatisticId}'.");
    }

    private async Task<JsonNode?> SendCommandAsync(JsonObject command, CancellationToken ct)
    {
        var id = ++_id;
        command["id"] = id;
        await SendRawAsync(command, ct);

        while (true)
        {
            var msg = await ReceiveJsonAsync(ct)
                      ?? throw new Exception("Home Assistant closed the connection while awaiting a result.");

            if (msg["type"]?.GetValue<string>() != "result")
                continue; // ignore events/pings not relevant to our sequential commands
            if (msg["id"]?.GetValue<int>() != id)
                continue;

            if (msg["success"]?.GetValue<bool>() != true)
            {
                var code = msg["error"]?["code"]?.GetValue<string>() ?? "";
                var message = msg["error"]?["message"]?.GetValue<string>() ?? "unknown error";
                throw new Exception($"Home Assistant command '{command["type"]}' failed: [{code}] {message}");
            }

            return msg["result"];
        }
    }

    private async Task SendRawAsync(JsonObject obj, CancellationToken ct)
    {
        var json = obj.ToJsonString();
        if (obj["type"]?.GetValue<string>() != "auth") // never log the token
            Log.Debug($"WS → {json}");
        await _ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);
    }

    private async Task<JsonNode?> ReceiveJsonAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        Log.Debug($"WS ← {json}");
        return JsonNode.Parse(json);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        }
        catch
        {
            // best-effort close; nothing actionable on a failing teardown
        }

        _ws.Dispose();
    }
}
