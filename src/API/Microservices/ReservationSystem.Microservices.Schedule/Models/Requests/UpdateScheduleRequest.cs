using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Schedule.Models.Requests;

/// <summary>
/// HTTP request body for PATCH /v1/schedules/{scheduleId} — updates the flights created count.
/// </summary>
public sealed class UpdateScheduleRequest
{
    [JsonPropertyName("flightsCreatedCount")]
    public int FlightsCreatedCount { get; init; }
}
