using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Requests;

public sealed class CreateSsrOptionRequest
{
    [JsonPropertyName("ssrCode")]
    public string SsrCode { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;
}
