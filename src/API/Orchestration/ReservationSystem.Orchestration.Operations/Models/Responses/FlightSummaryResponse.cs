using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class FlightSummaryResponse
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;
}
