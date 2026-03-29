using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

/// <summary>
/// Request body for POST /v1/schedules/import-inventory.
/// Supplies cabin seat counts for each aircraft type in the fleet so the handler
/// can match each schedule row to the correct configuration when generating inventory.
/// </summary>
public sealed class ImportSchedulesToInventoryRequest
{
    [JsonPropertyName("aircraftConfigs")]
    public IReadOnlyList<AircraftConfigRequest> AircraftConfigs { get; init; } = [];
}

public sealed class AircraftConfigRequest
{
    [JsonPropertyName("aircraftTypeCode")]
    public string AircraftTypeCode { get; init; } = string.Empty;

    [JsonPropertyName("cabins")]
    public IReadOnlyList<CabinSeatCountRequest> Cabins { get; init; } = [];
}

public sealed class CabinSeatCountRequest
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("totalSeats")]
    public int TotalSeats { get; init; }
}
