using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class FlightInventoryWithPinnedResponse
{
    [JsonPropertyName("flights")]
    public IReadOnlyList<FlightInventoryGroupResponse> Flights { get; init; } = [];

    [JsonPropertyName("pinnedFlights")]
    public IReadOnlyList<FlightInventoryGroupResponse> PinnedFlights { get; init; } = [];
}
