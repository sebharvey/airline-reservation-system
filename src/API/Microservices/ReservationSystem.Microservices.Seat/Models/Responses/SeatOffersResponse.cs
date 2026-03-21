namespace ReservationSystem.Microservices.Seat.Models.Responses;

/// <summary>
/// HTTP response body wrapping a list of seat offers for a flight.
/// </summary>
public sealed class SeatOffersResponse
{
    public Guid FlightId { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public IReadOnlyList<SeatOfferResponse> SeatOffers { get; init; } = [];
}
