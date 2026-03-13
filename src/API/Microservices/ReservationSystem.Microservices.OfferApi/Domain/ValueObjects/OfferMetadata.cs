namespace ReservationSystem.Microservices.OfferApi.Domain.ValueObjects;

/// <summary>
/// Value object representing additional structured metadata for an Offer.
/// Holds fields that are extensible or vary by fare type — stored as a JSON
/// column in the database. The domain only knows the typed representation here;
/// serialisation lives in Infrastructure.
/// </summary>
public sealed class OfferMetadata
{
    public string BaggageAllowance { get; }
    public bool IsRefundable { get; }
    public bool IsChangeable { get; }
    public int SeatsRemaining { get; }

    public OfferMetadata(
        string? baggageAllowance,
        bool isRefundable,
        bool isChangeable,
        int seatsRemaining)
    {
        BaggageAllowance = baggageAllowance ?? string.Empty;
        IsRefundable = isRefundable;
        IsChangeable = isChangeable;
        SeatsRemaining = seatsRemaining;
    }

    public static OfferMetadata Empty =>
        new(string.Empty, false, false, 0);

    public override bool Equals(object? obj)
    {
        if (obj is not OfferMetadata other) return false;

        return BaggageAllowance == other.BaggageAllowance
            && IsRefundable == other.IsRefundable
            && IsChangeable == other.IsChangeable
            && SeatsRemaining == other.SeatsRemaining;
    }

    public override int GetHashCode() =>
        HashCode.Combine(BaggageAllowance, IsRefundable, IsChangeable, SeatsRemaining);
}
