using System.Text.Json;
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

    [JsonPropertyName("cabinFareDefinitions")]
    public JsonElement? CabinFareDefinitions { get; init; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}
