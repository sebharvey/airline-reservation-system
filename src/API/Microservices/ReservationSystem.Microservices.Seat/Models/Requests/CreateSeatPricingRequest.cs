namespace ReservationSystem.Microservices.Seat.Models.Requests;

/// <summary>
/// HTTP request body for creating a new seat pricing rule.
/// </summary>
public sealed class CreateSeatPricingRequest
{
    public string CabinCode { get; init; } = string.Empty;
    public string SeatPosition { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
}
