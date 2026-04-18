namespace ReservationSystem.Microservices.Delivery.Domain.Services;

public enum TaxScope
{
    /// <summary>Tax applies to the coupon departing from a specific country.</summary>
    DepartureFromCountry,
    /// <summary>Tax applies to the coupon arriving in a specific country.</summary>
    ArrivalInCountry,
    /// <summary>Tax is split across every coupon (e.g. carrier surcharges).</summary>
    SplitPerCoupon,
    /// <summary>Tax applies to the ticket as a whole; attributed to all coupons.</summary>
    AllCoupons
}

/// <summary>Rule defining how a specific tax code is attributed to coupon(s).</summary>
public sealed record TaxRule(string TaxCode, TaxScope Scope, string? CountryCode = null);

/// <summary>Represents one coupon's itinerary position for attribution purposes.</summary>
public sealed record CouponItinerary(int CouponNumber, string Origin, string Destination);

/// <summary>
/// Assigns tax codes to coupon numbers using configurable rules.
/// Rules are data-driven so new tax codes can be added without code changes.
/// </summary>
public sealed class TaxAttributionService
{
    private static readonly IReadOnlyList<TaxRule> DefaultRules =
    [
        new TaxRule("GB", TaxScope.DepartureFromCountry, "GB"),   // UK APD — departure from UK
        new TaxRule("UB", TaxScope.DepartureFromCountry, "GB"),   // UK PSC — departure from UK
        new TaxRule("US", TaxScope.ArrivalInCountry,    "US"),    // US international transportation tax
        new TaxRule("XY", TaxScope.ArrivalInCountry,    "US"),    // US immigration
        new TaxRule("YC", TaxScope.ArrivalInCountry,    "US"),    // US customs
        new TaxRule("XA", TaxScope.ArrivalInCountry,    "US"),    // US APHIS
        new TaxRule("YQ", TaxScope.SplitPerCoupon,       null),   // Carrier fuel/security surcharge
        new TaxRule("YR", TaxScope.SplitPerCoupon,       null),   // Carrier surcharge (variant)
    ];

    private static readonly IReadOnlyDictionary<string, string> AirportCountry =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "LHR", "GB" }, { "LGW", "GB" }, { "MAN", "GB" }, { "STN", "GB" },
            { "BHX", "GB" }, { "GLA", "GB" }, { "EDI", "GB" }, { "BRS", "GB" },
            { "JFK", "US" }, { "EWR", "US" }, { "LAX", "US" }, { "ORD", "US" },
            { "ATL", "US" }, { "SFO", "US" }, { "BOS", "US" }, { "MIA", "US" },
            { "DFW", "US" }, { "SEA", "US" }, { "DEN", "US" }, { "IAD", "US" },
        };

    private readonly IReadOnlyList<TaxRule> _rules;

    public TaxAttributionService() : this(DefaultRules) { }

    public TaxAttributionService(IEnumerable<TaxRule> rules)
    {
        _rules = rules.ToList().AsReadOnly();
    }

    /// <summary>
    /// Returns the coupon numbers that <paramref name="taxCode"/> applies to,
    /// given the flight itinerary. Falls back to all coupons for unknown codes.
    /// </summary>
    public IReadOnlyList<int> GetCouponNumbers(string taxCode, IReadOnlyList<CouponItinerary> itinerary)
    {
        var all = itinerary.Select(c => c.CouponNumber).ToList();

        var rule = _rules.FirstOrDefault(r =>
            r.TaxCode.Equals(taxCode, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
            return all;

        return rule.Scope switch
        {
            TaxScope.DepartureFromCountry when rule.CountryCode is not null =>
                itinerary
                    .Where(c => GetCountry(c.Origin)
                        .Equals(rule.CountryCode, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.CouponNumber)
                    .ToList(),

            TaxScope.ArrivalInCountry when rule.CountryCode is not null =>
                itinerary
                    .Where(c => GetCountry(c.Destination)
                        .Equals(rule.CountryCode, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.CouponNumber)
                    .ToList(),

            _ => all
        };
    }

    /// <summary>
    /// Returns ready-to-store attribution groups for a single tax line.
    ///
    /// For <see cref="TaxScope.SplitPerCoupon"/> taxes (e.g. YQ carrier surcharge), the total
    /// amount is divided equally across the applicable coupons, producing one group per coupon.
    /// This ensures <c>GetAttributedValue</c> can sum TicketTax amounts without double-counting.
    ///
    /// For all other scopes, a single group is returned with the full amount.
    /// </summary>
    public IReadOnlyList<(decimal Amount, IReadOnlyList<int> CouponNumbers)> AttributeTax(
        string taxCode, decimal totalAmount, IReadOnlyList<CouponItinerary> itinerary)
    {
        var couponNumbers = GetCouponNumbers(taxCode, itinerary);
        if (couponNumbers.Count == 0)
            return [];

        var rule = _rules.FirstOrDefault(r =>
            r.TaxCode.Equals(taxCode, StringComparison.OrdinalIgnoreCase));

        if (rule?.Scope == TaxScope.SplitPerCoupon && couponNumbers.Count > 1)
        {
            // Distribute evenly; last coupon absorbs any rounding remainder.
            decimal perCoupon = Math.Round(totalAmount / couponNumbers.Count, 2, MidpointRounding.ToEven);
            decimal distributed = perCoupon * (couponNumbers.Count - 1);
            decimal last = totalAmount - distributed;

            return couponNumbers
                .Select((n, idx) => (
                    Amount: idx < couponNumbers.Count - 1 ? perCoupon : last,
                    CouponNumbers: (IReadOnlyList<int>)new[] { n }))
                .ToList();
        }

        return [(totalAmount, couponNumbers)];
    }

    public string GetCountry(string airportCode) =>
        AirportCountry.TryGetValue(airportCode, out var country) ? country : airportCode;
}
