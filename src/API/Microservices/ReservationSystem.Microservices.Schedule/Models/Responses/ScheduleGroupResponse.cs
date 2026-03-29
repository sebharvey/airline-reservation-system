using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Schedule.Models.Responses;

public sealed class GetScheduleGroupsResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<ScheduleGroupItem> Groups { get; init; } = [];
}

public sealed class ScheduleGroupItem
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
