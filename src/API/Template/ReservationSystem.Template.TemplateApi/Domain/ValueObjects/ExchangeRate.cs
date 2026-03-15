namespace ReservationSystem.Template.TemplateApi.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a currency exchange rate between two ISO 4217 currencies.
/// Equality is based on value, not identity.
/// </summary>
public sealed class ExchangeRate : IEquatable<ExchangeRate>
{
    /// <summary>The source currency code (e.g. "USD").</summary>
    public string FromCurrency { get; }

    /// <summary>The target currency code (e.g. "GBP").</summary>
    public string ToCurrency { get; }

    /// <summary>
    /// Number of <see cref="ToCurrency"/> units per one <see cref="FromCurrency"/> unit.
    /// </summary>
    public decimal Rate { get; }

    /// <summary>UTC timestamp at which this rate was retrieved from the provider.</summary>
    public DateTimeOffset FetchedAt { get; }

    public ExchangeRate(string fromCurrency, string toCurrency, decimal rate, DateTimeOffset fetchedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(toCurrency);

        if (rate <= 0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Exchange rate must be positive.");

        FromCurrency = fromCurrency.ToUpperInvariant();
        ToCurrency = toCurrency.ToUpperInvariant();
        Rate = rate;
        FetchedAt = fetchedAt;
    }

    /// <summary>Converts an amount using this exchange rate.</summary>
    public decimal Convert(decimal amount) => amount * Rate;

    public bool Equals(ExchangeRate? other) =>
        other is not null &&
        FromCurrency == other.FromCurrency &&
        ToCurrency == other.ToCurrency &&
        Rate == other.Rate &&
        FetchedAt == other.FetchedAt;

    public override bool Equals(object? obj) => Equals(obj as ExchangeRate);

    public override int GetHashCode() =>
        HashCode.Combine(FromCurrency, ToCurrency, Rate, FetchedAt);

    public override string ToString() => $"{FromCurrency}/{ToCurrency} @ {Rate}";
}
