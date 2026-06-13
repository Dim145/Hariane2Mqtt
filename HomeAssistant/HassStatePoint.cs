namespace Hariane2Mqtt.HomeAssistant;

/// <summary>
/// A single long-term-statistics data point to import into Home Assistant.
/// <see cref="Start"/> is the (timezone-aware, top-of-hour) instant the point applies to,
/// <see cref="Sum"/> the cumulative running total and <see cref="State"/> the (optional) reading.
/// </summary>
public readonly record struct HassStatePoint(DateTimeOffset Start, double Sum, double? State);
