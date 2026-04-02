using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

public sealed class UpdateSsrOptionRequest
{
    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;
}
