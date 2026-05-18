using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminDisruptionTimeRequest
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("newDepartureTime")]
    public string NewDepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("newArrivalTime")]
    public string NewArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("newArrivalDayOffset")]
    public int NewArrivalDayOffset { get; init; }

    [JsonPropertyName("newDepartureTimeUtc")]
    public string? NewDepartureTimeUtc { get; init; }

    [JsonPropertyName("newArrivalTimeUtc")]
    public string? NewArrivalTimeUtc { get; init; }

    [JsonPropertyName("newArrivalDayOffsetUtc")]
    public int? NewArrivalDayOffsetUtc { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}
