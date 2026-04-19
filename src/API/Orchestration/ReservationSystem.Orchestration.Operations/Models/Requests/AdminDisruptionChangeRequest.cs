using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminDisruptionChangeRequest
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("newAircraftType")]
    public string NewAircraftType { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
