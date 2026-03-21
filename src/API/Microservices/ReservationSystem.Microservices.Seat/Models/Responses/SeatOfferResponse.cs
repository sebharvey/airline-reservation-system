namespace ReservationSystem.Microservices.Seat.Models.Responses;

/// <summary>
/// HTTP response body representing a single seat offer.
/// </summary>
public sealed class SeatOfferResponse
{
    public string SeatOfferId { get; init; } = string.Empty;
    public Guid FlightId { get; init; }
    public string SeatNumber { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string SeatPosition { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
}
