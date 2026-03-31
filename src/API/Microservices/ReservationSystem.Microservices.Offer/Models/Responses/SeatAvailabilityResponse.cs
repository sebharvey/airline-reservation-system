using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class SeatAvailabilityItem
{
    [JsonPropertyName("seatOfferId")]
    public Guid SeatOfferId { get; init; }

    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}

public sealed class SeatAvailabilityResponse
{
    [JsonPropertyName("flightId")]
    public Guid FlightId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("seatAvailability")]
    public IReadOnlyList<SeatAvailabilityItem> SeatAvailability { get; init; } = [];
}

public sealed class ReserveSeatResponse
{
    [JsonPropertyName("flightId")]
    public Guid FlightId { get; init; }

    [JsonPropertyName("reserved")]
    public IReadOnlyList<string> Reserved { get; init; } = [];
}

public sealed class UpdateSeatStatusResponse
{
    [JsonPropertyName("updated")]
    public int Updated { get; init; }
}
