namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Responses;

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
    public decimal Tax { get; init; }
    public bool IsActive { get; init; }
    public DateTime ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
