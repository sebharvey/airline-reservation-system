using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class FlightStatusResponse
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("scheduledDepartureDateTime")]
    public string ScheduledDepartureDateTime { get; init; } = string.Empty;

    [JsonPropertyName("scheduledArrivalDateTime")]
    public string ScheduledArrivalDateTime { get; init; } = string.Empty;

    [JsonPropertyName("estimatedDepartureDateTime")]
    public string? EstimatedDepartureDateTime { get; init; }

    [JsonPropertyName("estimatedArrivalDateTime")]
    public string? EstimatedArrivalDateTime { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("gate")]
    public string? Gate { get; init; }

    [JsonPropertyName("terminal")]
    public string? Terminal { get; init; }

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("delayMinutes")]
    public int DelayMinutes { get; init; }

    [JsonPropertyName("statusMessage")]
    public string StatusMessage { get; init; } = string.Empty;
}
