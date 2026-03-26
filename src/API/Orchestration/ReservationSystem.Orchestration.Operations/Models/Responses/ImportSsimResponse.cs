using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/schedules/ssim (200 OK).
/// Mirrors the Schedule MS response.
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
