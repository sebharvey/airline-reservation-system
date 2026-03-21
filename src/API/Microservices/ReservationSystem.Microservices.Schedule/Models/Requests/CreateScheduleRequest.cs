using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Schedule.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/schedules.
/// </summary>
public sealed class CreateScheduleRequest
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public DateTimeOffset ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTimeOffset ValidTo { get; init; }
}
