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

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;
}
