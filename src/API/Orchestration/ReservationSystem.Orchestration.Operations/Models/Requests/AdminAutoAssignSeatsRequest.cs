using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminAutoAssignSeatsRequest
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;
}
