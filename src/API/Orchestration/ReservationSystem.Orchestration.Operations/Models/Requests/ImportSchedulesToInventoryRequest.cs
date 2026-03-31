using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

/// <summary>
/// Request body for POST /v1/schedules/import-inventory.
/// </summary>
public sealed class ImportSchedulesToInventoryRequest
{
    [JsonPropertyName("scheduleGroupId")]
    public Guid? ScheduleGroupId { get; init; }

    /// <summary>
    /// Optional upper date bound (ISO 8601, e.g. "2026-06-30"). No inventory will be
    /// created beyond this date. The terminal passes today + 3 months to limit the
    /// initial bulk import window.
    /// </summary>
    [JsonPropertyName("toDate")]
    public string? ToDate { get; init; }
}
