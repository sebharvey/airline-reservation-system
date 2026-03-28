using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/schedules/import-inventory (200 OK).
/// </summary>
public sealed class ImportSchedulesToInventoryResponse
{
    [JsonPropertyName("schedulesProcessed")]
    public int SchedulesProcessed { get; init; }

    [JsonPropertyName("inventoriesCreated")]
    public int InventoriesCreated { get; init; }

    [JsonPropertyName("inventoriesSkipped")]
    public int InventoriesSkipped { get; init; }

    [JsonPropertyName("faresCreated")]
    public int FaresCreated { get; init; }
}
