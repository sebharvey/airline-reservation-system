using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class BatchCreateFlightsResponse
{
    [JsonPropertyName("created")]
    public int Created { get; init; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; init; }

    [JsonPropertyName("inventories")]
    public IReadOnlyList<FlightInventoryResponse> Inventories { get; init; } = [];
}
