using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

/// <summary>
/// Request body for POST /v1/schedules/import-inventory.
/// </summary>
public sealed class ImportSchedulesToInventoryRequest
{
    [JsonPropertyName("scheduleGroupId")]
    public Guid? ScheduleGroupId { get; init; }
}
