using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Schedule.Models.Responses;

/// <summary>
/// HTTP response body for Schedule endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class ScheduleResponse
{
    [JsonPropertyName("scheduleId")]
    public Guid ScheduleId { get; init; }

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

    [JsonPropertyName("flightsCreatedCount")]
    public int FlightsCreatedCount { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}
