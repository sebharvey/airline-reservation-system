using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

/// <summary>
/// HTTP response body for GET /v1/schedules (200 OK).
/// Returns all stored flight schedules from the Schedule MS.
/// </summary>
public sealed class GetSchedulesResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("schedules")]
    public IReadOnlyList<ScheduleSummary> Schedules { get; init; } = [];
}

public sealed class ScheduleSummary
{
    [JsonPropertyName("scheduleId")]
    public Guid ScheduleId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalDayOffset")]
    public int ArrivalDayOffset { get; init; }

    [JsonPropertyName("daysOfWeek")]
    public int DaysOfWeek { get; init; }

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public string ValidFrom { get; init; } = string.Empty;

    [JsonPropertyName("validTo")]
    public string ValidTo { get; init; } = string.Empty;

    [JsonPropertyName("flightsCreated")]
    public int FlightsCreated { get; init; }

    [JsonPropertyName("operatingDateCount")]
    public int OperatingDateCount { get; init; }
}
