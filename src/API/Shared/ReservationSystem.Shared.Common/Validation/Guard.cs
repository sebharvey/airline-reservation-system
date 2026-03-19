namespace ReservationSystem.Shared.Common.Validation;

/// <summary>
/// Lightweight guard-clause helpers for validating command and query inputs at
/// the boundary of each application handler.
///
/// Guards are the first line of defence after deserialization: they fail fast
/// with a clear <see cref="ArgumentException"/> rather than letting invalid
/// data propagate into domain logic or SQL queries.
///
/// All methods return the validated value so guards can be inlined:
/// <code>
///   public sealed record CreateOrderCommand(
///       Guid     OfferId  = Guard.NotEmpty(offerId,  nameof(offerId)),
///       string   Currency = Guard.NotNullOrEmpty(currency, nameof(currency)),
///       decimal  Amount   = Guard.Positive(amount, nameof(amount)));
/// </code>
///
/// Or in a handler constructor:
/// <code>
///   public CreateOrderHandler(IOrderRepository repo)
///   {
///       _repo = Guard.NotNull(repo, nameof(repo));
///   }
/// </code>
///
/// Throws <see cref="ArgumentNullException"/> or <see cref="ArgumentException"/>
/// depending on the violation, which the function host surfaces as a 500.
/// Convert to 400/422 responses at the function layer by catching those
/// exceptions there, or add a middleware try/catch around handler.HandleAsync().
/// </summary>
public static class Guard
{
    // -------------------------------------------------------------------------
    // Null / empty checks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> if <paramref name="value"/>
    /// is <see langword="null"/>; otherwise returns the value.
    /// </summary>
    public static T NotNull<T>(T? value, string paramName) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> if <paramref name="value"/>
    /// is <see langword="null"/>, or <see cref="ArgumentException"/> if it is
    /// empty or whitespace-only; otherwise returns the trimmed string.
    /// </summary>
    public static string NotNullOrEmpty(string? value, string paramName)
    {
        if (value is null)
            throw new ArgumentNullException(paramName);

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Value must not be empty or whitespace.", paramName);

        return trimmed;
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> is
    /// <see cref="Guid.Empty"/>; otherwise returns the value.
    /// Use for ID fields that must refer to a real record.
    /// </summary>
    public static Guid NotEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("GUID must not be empty.", paramName);

        return value;
    }

    // -------------------------------------------------------------------------
    // Numeric / comparable checks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/>
    /// is not strictly greater than zero; otherwise returns the value.
    /// Use for prices, quantities, seat counts, durations, etc.
    /// </summary>
    public static T Positive<T>(T value, string paramName) where T : IComparable<T>
    {
        if (value.CompareTo(default) <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");

        return value;
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/>
    /// falls outside the inclusive range [<paramref name="min"/>, <paramref name="max"/>];
    /// otherwise returns the value.
    /// Use for things like cabin codes, page sizes, baggage weights.
    /// </summary>
    public static T InRange<T>(T value, T min, T max, string paramName) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(
                paramName, value, $"Value must be between {min} and {max} (inclusive).");

        return value;
    }

    // -------------------------------------------------------------------------
    // Date / time checks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="value"/> is in
    /// the past relative to <see cref="DateTimeOffset.UtcNow"/>; otherwise returns
    /// the value.
    /// Use to validate departure dates, offer expiry dates, etc.
    /// </summary>
    public static DateTimeOffset NotInPast(DateTimeOffset value, string paramName)
    {
        if (value < DateTimeOffset.UtcNow)
            throw new ArgumentException("Date must not be in the past.", paramName);

        return value;
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="end"/> is not
    /// strictly after <paramref name="start"/>; otherwise returns <paramref name="end"/>.
    /// Use to validate date ranges (e.g. check-in / check-out, search windows).
    /// </summary>
    public static DateTimeOffset After(DateTimeOffset end, DateTimeOffset start, string paramName)
    {
        if (end <= start)
            throw new ArgumentException(
                $"End date ({end:O}) must be strictly after start date ({start:O}).", paramName);

        return end;
    }
}
