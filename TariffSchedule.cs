using System.Globalization;

namespace Hariane2Mqtt;

/// <summary>One recorded tariff: a price effective from a date (until the next entry).</summary>
public readonly record struct TariffEntry(DateOnly Date, float Price);

/// <summary>
/// Water price schedule: each price applies from its effective date until the next one
/// (changes are NOT retroactive). Parsed from <c>PRICE_PER_M3</c>: a flat number ("4.30")
/// or dated entries ("2023-01-01:3.50,2025-06-01:4.30"). Blank / "0" → no cost tracking.
///
/// A flat number enables "auto mode": <see cref="FlatPrice"/> is set, and the caller records
/// each change with its date via <see cref="RecordFlatChange"/> so past days keep their price.
/// </summary>
public class TariffSchedule
{
    private readonly List<(DateOnly From, float Price)> _tariffs;

    /// <summary>Set when the spec was a single bare number (auto-record mode); null otherwise.</summary>
    public float? FlatPrice { get; }

    private TariffSchedule(List<(DateOnly, float)> tariffs, float? flatPrice)
    {
        _tariffs = tariffs;
        FlatPrice = flatPrice;
    }

    public bool IsEmpty => _tariffs.Count == 0;

    public static TariffSchedule Parse(string? spec)
    {
        var tariffs = new List<(DateOnly, float)>();
        var hadDate = false;

        if (!string.IsNullOrWhiteSpace(spec))
        {
            foreach (var raw in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var sep = raw.IndexOf(':');
                var from = DateOnly.MinValue;
                var priceText = raw;

                if (sep >= 0)
                {
                    hadDate = true;
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

        var flat = !hadDate && tariffs.Count == 1 ? tariffs[0].Item2 : (float?)null;
        return new TariffSchedule(tariffs, flat);
    }

    /// <summary>Builds a schedule from recorded entries (auto-record mode).</summary>
    public static TariffSchedule FromEntries(IEnumerable<TariffEntry> entries)
    {
        var tariffs = entries.Select(e => (e.Date, e.Price)).OrderBy(t => t.Date).ToList();
        return new TariffSchedule(tariffs, flatPrice: null);
    }

    /// <summary>
    /// Records a flat-price reading: the first price applies to the whole history (from MinValue);
    /// a later, different price is stamped from <paramref name="today"/>. Returns the updated history.
    /// </summary>
    public static List<TariffEntry> RecordFlatChange(IReadOnlyList<TariffEntry> history, float price, DateOnly today)
    {
        var list = new List<TariffEntry>(history);
        if (list.Count == 0)
            list.Add(new TariffEntry(DateOnly.MinValue, price)); // first price → whole history
        else if (MathF.Abs(list[^1].Price - price) > 0.0001f)
            list.Add(new TariffEntry(today, price)); // change → effective from today
        return list;
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
