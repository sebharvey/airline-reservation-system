using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class AdminDisruptionChangeResponse
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("newAircraftType")]
    public string NewAircraftType { get; init; } = string.Empty;

    [JsonPropertyName("processedAt")]
    public DateTime ProcessedAt { get; init; }
}
