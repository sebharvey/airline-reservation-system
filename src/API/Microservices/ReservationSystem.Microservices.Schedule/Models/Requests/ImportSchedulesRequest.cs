using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Schedule.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/schedules.
/// Accepts the full IATA-structured season schedule payload.
/// </summary>
public sealed class ImportSchedulesRequest
{
    [JsonPropertyName("scheduleGroupId")]
    public Guid ScheduleGroupId { get; init; }

    [JsonPropertyName("header")]
    public ScheduleHeaderRequest? Header { get; init; }

    [JsonPropertyName("carriers")]
    public IReadOnlyList<CarrierSchedulesRequest> Carriers { get; init; } = [];

    [JsonPropertyName("recordCount")]
    public int RecordCount { get; init; }
}

public sealed class ScheduleHeaderRequest
{
    [JsonPropertyName("standard")]
    public string Standard { get; init; } = string.Empty;

    [JsonPropertyName("airlineCode")]
    public string AirlineCode { get; init; } = string.Empty;

    [JsonPropertyName("seasonStart")]
    public string SeasonStart { get; init; } = string.Empty;

    [JsonPropertyName("seasonEnd")]
    public string SeasonEnd { get; init; } = string.Empty;

    [JsonPropertyName("fileType")]
    public string FileType { get; init; } = string.Empty;

    [JsonPropertyName("fileCreationDate")]
    public string FileCreationDate { get; init; } = string.Empty;
}

public sealed class CarrierSchedulesRequest
{
    [JsonPropertyName("airlineCode")]
    public string AirlineCode { get; init; } = string.Empty;

    [JsonPropertyName("seasonCode")]
    public string SeasonCode { get; init; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public string ValidFrom { get; init; } = string.Empty;

    [JsonPropertyName("validTo")]
    public string ValidTo { get; init; } = string.Empty;

    [JsonPropertyName("schedules")]
    public IReadOnlyList<FlightScheduleRequest> Schedules { get; init; } = [];
}

public sealed class FlightScheduleRequest
{
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

    [JsonPropertyName("departureTimeUtc")]
    public string? DepartureTimeUtc { get; init; }

    [JsonPropertyName("arrivalTimeUtc")]
    public string? ArrivalTimeUtc { get; init; }

    [JsonPropertyName("arrivalDayOffsetUtc")]
    public byte? ArrivalDayOffsetUtc { get; init; }

    [JsonPropertyName("daysOfWeek")]
    public byte DaysOfWeek { get; init; }

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("validFrom")]
    public string ValidFrom { get; init; } = string.Empty;

    [JsonPropertyName("validTo")]
    public string ValidTo { get; init; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;
}
