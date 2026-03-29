using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class GetScheduleGroupsResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<ScheduleGroupSummary> Groups { get; init; } = [];
}

public sealed class ScheduleGroupSummary
{
    [JsonPropertyName("scheduleGroupId")]
    public Guid ScheduleGroupId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("seasonStart")]
    public string SeasonStart { get; init; } = string.Empty;

    [JsonPropertyName("seasonEnd")]
    public string SeasonEnd { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("scheduleCount")]
    public int ScheduleCount { get; init; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; init; } = string.Empty;
}
