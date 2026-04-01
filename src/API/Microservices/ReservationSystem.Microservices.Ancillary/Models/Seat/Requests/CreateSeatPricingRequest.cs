namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Requests;

/// <summary>
/// HTTP request body for creating a new seat pricing rule.
/// </summary>
public sealed class CreateSeatPricingRequest
{
    public string CabinCode { get; init; } = string.Empty;
    public string SeatPosition { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime ValidFrom { get; init; }
    public DateTime? ValidTo { get; init; }
}
