using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Responses;

/// <summary>
/// Response for GET /v1/seat-offers?flightId={flightId} — all seat offers for a flight.
/// </summary>
public sealed class SeatOffersResponse
{
    [JsonPropertyName("flightId")]
    public Guid FlightId { get; init; }

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("seatOffers")]
    public IReadOnlyList<SeatOfferResponse> SeatOffers { get; init; } = [];
}
