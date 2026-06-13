using Hariane2Mqtt.HomeAssistant;

namespace Hariane2Mqtt.Tests;

public class StatisticsMetadataTests
{
    [Theory]
    [InlineData("123456789", "hariane:water_123456789")]
    [InlineData("ABC-123", "hariane:water_abc_123")]
    [InlineData("a__b", "hariane:water_a_b")]
    [InlineData("   ", "hariane:water_meter")]
    public void BuildStatisticId_ProducesExpectedSlug(string contrat, string expected)
        => Assert.Equal(expected, StatisticsMetadata.BuildStatisticId(contrat));

    [Theory]
    [InlineData("123456789")]
    [InlineData("Contrat 42/Eau")]
    [InlineData("--__--")]
    public void StatisticId_MatchesHomeAssistantExternalIdRules(string contrat)
    {
        var id = StatisticsMetadata.BuildStatisticId(contrat);

        // HA external statistic id: domain:object_id, lowercase [a-z0-9_], no '__', no leading/trailing '_'.
        Assert.Matches(@"^(?!.+__)(?!_)[\da-z_]+(?<!_):(?!_)[\da-z_]+(?<!_)$", id);
        Assert.StartsWith("hariane:water_", id);
    }
}
