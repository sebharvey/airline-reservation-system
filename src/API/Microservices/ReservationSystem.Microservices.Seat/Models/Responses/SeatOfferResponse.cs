namespace ReservationSystem.Microservices.Seat.Models.Responses;

/// <summary>
/// HTTP response body representing a single seat offer.
/// </summary>
public sealed class SeatOfferResponse
{
    public string SeatOfferId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public List<string> Attributes { get; init; } = [];
    public bool IsSelectable { get; init; }
    public bool IsChargeable { get; init; }
    public decimal Price { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsValid { get; init; }
}
