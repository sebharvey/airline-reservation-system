using System.Globalization;

namespace ReservationSystem.Microservices.Delivery.Domain.ValueObjects;

/// <summary>
/// Parses and validates an IATA linear fare calculation string, e.g.:
///   LON BA NYC 500.00 BA LON 500.00 NUC1000.00 END ROE0.800000
///
/// The string is the authoritative representation. Components are derived on demand;
/// coupon-level fare share is calculated proportionally from each component's NUC weight.
/// </summary>
public sealed class FareCalculation
{
    public string Raw { get; }
    public IReadOnlyList<FareComponent> Components { get; }
    public decimal TotalNuc { get; }
    public decimal Roe { get; }

    private FareCalculation(string raw, IReadOnlyList<FareComponent> components, decimal totalNuc, decimal roe)
    {
        Raw = raw;
        Components = components;
        TotalNuc = totalNuc;
        Roe = roe;
    }

    /// <summary>
    /// Attempts to parse <paramref name="fareCalcString"/> into a <see cref="FareCalculation"/>.
    /// Returns <c>false</c> and sets <paramref name="error"/> on failure.
    /// </summary>
    public static bool TryParse(string fareCalcString, out FareCalculation? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(fareCalcString))
        {
            error = "Fare calculation string is empty.";
            return false;
        }

        try
        {
            var tokens = fareCalcString.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var components = new List<FareComponent>();
            decimal totalNuc = 0;
            decimal roe = 0;
            int i = 0;

            // First token must be a city code.
            if (i >= tokens.Length || !IsCityCode(tokens[i]))
            {
                error = $"Expected 3-letter city code at start, got '{(tokens.Length > 0 ? tokens[0] : "<empty>")}'.";
                return false;
            }

            string currentOrigin = tokens[i++];

            while (i < tokens.Length)
            {
                string token = tokens[i];

                // NUC total signals the end of fare components.
                if (token.StartsWith("NUC", StringComparison.OrdinalIgnoreCase))
                {
                    string nucStr = token[3..];
                    if (!decimal.TryParse(nucStr, NumberStyles.Number, CultureInfo.InvariantCulture, out totalNuc))
                    {
                        error = $"Invalid NUC amount: '{nucStr}'.";
                        return false;
                    }
                    i++;

                    // Optional END marker.
                    if (i < tokens.Length && tokens[i].Equals("END", StringComparison.OrdinalIgnoreCase))
                        i++;

                    // ROE.
                    if (i < tokens.Length && tokens[i].StartsWith("ROE", StringComparison.OrdinalIgnoreCase))
                    {
                        string roeStr = tokens[i][3..];
                        if (!decimal.TryParse(roeStr, NumberStyles.Number, CultureInfo.InvariantCulture, out roe))
                        {
                            error = $"Invalid ROE value: '{roeStr}'.";
                            return false;
                        }
                        i++;
                    }

                    break;
                }

                // Expect: <CarrierCode> <DestCity> <Amount> [Q<surcharge>]
                if (!IsCarrierCode(token))
                {
                    error = $"Expected 2-letter carrier code, got '{token}'.";
                    return false;
                }
                string carrier = token; i++;

                if (i >= tokens.Length || !IsCityCode(tokens[i]))
                {
                    error = $"Expected destination city code after carrier '{carrier}'.";
                    return false;
                }
                string destination = tokens[i]; i++;

                decimal componentAmount = 0;
                if (i < tokens.Length &&
                    decimal.TryParse(tokens[i], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
                {
                    componentAmount = parsed;
                    i++;
                }

                // Optional Q surcharge (e.g. Q25.00) — skip, it's already in the NUC amount.
                if (i < tokens.Length &&
                    tokens[i].Length > 1 &&
                    tokens[i][0] is 'Q' or 'q' &&
                    char.IsDigit(tokens[i][1]))
                {
                    i++;
                }

                components.Add(new FareComponent(currentOrigin, carrier, destination, componentAmount));
                currentOrigin = destination;
            }

            if (components.Count == 0)
            {
                error = "No fare components found.";
                return false;
            }

            // Validate: component NUC sum must match declared total NUC (within 0.01 for rounding).
            if (totalNuc > 0)
            {
                decimal componentSum = components.Sum(c => c.NucAmount);
                if (Math.Abs(componentSum - totalNuc) > 0.01m)
                {
                    error = $"Component NUC sum {componentSum:F2} does not match declared total NUC {totalNuc:F2}.";
                    return false;
                }
            }

            result = new FareCalculation(fareCalcString, components.AsReadOnly(), totalNuc, roe);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Fare calculation parse error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Returns the proportional local-currency fare share for coupon at 1-based <paramref name="couponNumber"/>,
    /// using the ticket's <paramref name="totalLocalFare"/> as the authoritative total.
    /// </summary>
    public decimal GetFareShareForCoupon(int couponNumber, decimal totalLocalFare)
    {
        if (Components.Count == 0 || TotalNuc == 0 || couponNumber < 1 || couponNumber > Components.Count)
            return 0;

        decimal nucShare = Components[couponNumber - 1].NucAmount;
        return Math.Round(nucShare / TotalNuc * totalLocalFare, 2, MidpointRounding.AwayFromZero);
    }

    private static bool IsCityCode(string token) =>
        token.Length == 3 && token.All(char.IsLetter);

    private static bool IsCarrierCode(string token) =>
        token.Length == 2 && token.All(char.IsLetter);
}
