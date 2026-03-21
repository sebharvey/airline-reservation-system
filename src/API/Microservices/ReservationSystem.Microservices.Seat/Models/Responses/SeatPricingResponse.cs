namespace ReservationSystem.Microservices.Seat.Models.Responses;

/// <summary>
/// HTTP response body representing a seat pricing rule.
/// </summary>
public sealed class SeatPricingResponse
{
    public Guid SeatPricingId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public string SeatPosition { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
