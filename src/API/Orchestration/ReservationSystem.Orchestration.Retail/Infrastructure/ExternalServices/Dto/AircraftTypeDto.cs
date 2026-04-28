using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;

public sealed class AircraftTypeDto
{
    [JsonPropertyName("aircraftTypeCode")]
    public string AircraftTypeCode { get; init; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; init; } = string.Empty;

    [JsonPropertyName("friendlyName")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}

internal sealed record AircraftTypeListWrapper(
    [property: JsonPropertyName("aircraftTypes")] IReadOnlyList<AircraftTypeDto> AircraftTypes);
