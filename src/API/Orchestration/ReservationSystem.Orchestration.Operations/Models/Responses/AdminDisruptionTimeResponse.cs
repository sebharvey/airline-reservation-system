using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class AdminDisruptionTimeResponse
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("newDepartureTime")]
    public string NewDepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("newArrivalTime")]
    public string NewArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("processedAt")]
    public DateTime ProcessedAt { get; init; }
}
