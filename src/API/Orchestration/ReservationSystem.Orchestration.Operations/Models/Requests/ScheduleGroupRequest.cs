using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class CreateScheduleGroupRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("seasonStart")]
    public string SeasonStart { get; init; } = string.Empty;

    [JsonPropertyName("seasonEnd")]
    public string SeasonEnd { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;
}

public sealed class UpdateScheduleGroupRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("seasonStart")]
    public string SeasonStart { get; init; } = string.Empty;

    [JsonPropertyName("seasonEnd")]
    public string SeasonEnd { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
