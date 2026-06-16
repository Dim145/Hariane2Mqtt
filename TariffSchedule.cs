using System.Globalization;

namespace Hariane2Mqtt;

/// <summary>
/// Water price schedule: each price applies from its effective date until the next one
/// (changes are NOT retroactive). Parsed from <c>PRICE_PER_M3</c>: a flat number ("4.30")
/// or dated entries ("2023-01-01:3.50,2025-06-01:4.30"). Blank / "0" → no cost tracking.
/// </summary>
public class TariffSchedule
{
    private readonly List<(DateOnly From, float Price)> _tariffs;

    private TariffSchedule(List<(DateOnly, float)> tariffs) => _tariffs = tariffs;

    public bool IsEmpty => _tariffs.Count == 0;

    public static TariffSchedule Parse(string? spec)
    {
        var tariffs = new List<(DateOnly, float)>();

        if (!string.IsNullOrWhiteSpace(spec))
        {
            foreach (var raw in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var sep = raw.IndexOf(':');
                var from = DateOnly.MinValue;
                var priceText = raw;

                if (sep >= 0)
                {
                    from = DateOnly.ParseExact(raw[..sep].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    priceText = raw[(sep + 1)..].Trim();
                }

                tariffs.Add((from, float.Parse(priceText, CultureInfo.InvariantCulture)));
            }
        }

        tariffs.Sort((a, b) => a.Item1.CompareTo(b.Item1));

        // a lone "0" means "disabled"
        if (tariffs.Count == 1 && tariffs[0].Item2 == 0f)
            tariffs.Clear();

        return new TariffSchedule(tariffs);
    }

    /// <summary>Price in effect on <paramref name="day"/> — the latest tariff with From ≤ day, or 0 if none.</summary>
    public float PriceOn(DateOnly day)
    {
        var price = 0f;
        foreach (var (from, p) in _tariffs)
        {
            if (from > day) break;
            price = p;
        }
        return price;
    }
}
