using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Schedule.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/schedules (200 OK).
/// Returns the count of schedules imported, deleted, and a per-schedule summary.
/// </summary>
public sealed class ImportSchedulesResponse
{
    [JsonPropertyName("imported")]
    public int Imported { get; init; }

    [JsonPropertyName("deleted")]
    public int Deleted { get; init; }

    [JsonPropertyName("schedules")]
    public IReadOnlyList<ImportedScheduleSummary> Schedules { get; init; } = [];
}

public sealed class ImportedScheduleSummary
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
