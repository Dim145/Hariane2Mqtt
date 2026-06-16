namespace Hariane2Mqtt;

/// <summary>Run-wide settings parsed from the environment, shared by every contract.</summary>
public record RunConfig
{
    public required string MqttHost { get; init; }
    public required string MqttPort { get; init; }
    public required string MqttClientId { get; init; }
    public required string MqttUsername { get; init; }
    public required string MqttPassword { get; init; }
    public required string MqttTopic { get; init; }

    public required bool CalculateTotal { get; init; }
    public required bool ImportStats { get; init; }
    public required TariffSchedule Tariff { get; init; }

    public required bool LeakDetection { get; init; }
    public required int LeakMinDays { get; init; }
    public required float LeakThreshold { get; init; }

    public required string DirectoryForData { get; init; }
}
