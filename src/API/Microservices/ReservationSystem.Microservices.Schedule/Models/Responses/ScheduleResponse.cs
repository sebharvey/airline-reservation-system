using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Schedule.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/schedules.
/// Matches the API specification response contract.
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

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalDayOffset")]
    public byte ArrivalDayOffset { get; init; }

    [JsonPropertyName("daysOfWeek")]
    public byte DaysOfWeek { get; init; }

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public string ValidFrom { get; init; } = string.Empty;

    [JsonPropertyName("validTo")]
    public string ValidTo { get; init; } = string.Empty;

    [JsonPropertyName("flightsCreated")]
    public int FlightsCreated { get; init; }

    [JsonPropertyName("operatingDates")]
    public IReadOnlyList<string> OperatingDates { get; init; } = [];

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// HTTP response body for POST /v1/schedules (201 Created).
/// Returns scheduleId and the list of operating dates within the schedule window.
/// </summary>
public sealed class CreateScheduleResponse
{
    [JsonPropertyName("scheduleId")]
    public Guid ScheduleId { get; init; }

    [JsonPropertyName("operatingDates")]
    public IReadOnlyList<string> OperatingDates { get; init; } = [];
}

/// <summary>
/// HTTP response body for PATCH /v1/schedules/{scheduleId} (200 OK).
/// Spec defines only 2 fields: scheduleId, flightsCreated.
/// </summary>
public sealed class UpdateScheduleResponse
{
    [JsonPropertyName("scheduleId")]
    public Guid ScheduleId { get; init; }

    [JsonPropertyName("flightsCreated")]
    public int FlightsCreated { get; init; }
}

/// <summary>
/// HTTP response body for POST /v1/schedules/ssim (200 OK).
/// Returns the count and summary of each schedule created from the SSIM import.
/// </summary>
public sealed class ImportSsimResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("schedules")]
    public IReadOnlyList<ImportedScheduleItem> Schedules { get; init; } = [];
}

public sealed class ImportedScheduleItem
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
    public string ValidFrom { get; init; } = string.Empty;

    [JsonPropertyName("validTo")]
    public string ValidTo { get; init; } = string.Empty;

    [JsonPropertyName("operatingDateCount")]
    public int OperatingDateCount { get; init; }
}
