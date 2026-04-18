namespace ReservationSystem.Microservices.Delivery.Domain.ValueObjects;

/// <summary>
/// Derived value for a single flight coupon. Computed from the fare construction and tax breakdown;
/// never stored as an authoritative persisted amount.
/// </summary>
public sealed record CouponValue(
    int CouponNumber,
    decimal FareShare,
    decimal TaxShare,
    decimal Total,
    string Currency
);
