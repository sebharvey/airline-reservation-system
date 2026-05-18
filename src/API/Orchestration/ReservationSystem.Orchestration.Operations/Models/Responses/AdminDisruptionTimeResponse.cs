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

    [JsonPropertyName("newDepartureTimeUtc")]
    public string? NewDepartureTimeUtc { get; init; }

    [JsonPropertyName("newArrivalTimeUtc")]
    public string? NewArrivalTimeUtc { get; init; }

    [JsonPropertyName("inventoriesUpdated")]
    public int InventoriesUpdated { get; init; }

    [JsonPropertyName("affectedPassengerCount")]
    public int AffectedPassengerCount { get; init; }

    [JsonPropertyName("processedAt")]
    public DateTime ProcessedAt { get; init; }
}
