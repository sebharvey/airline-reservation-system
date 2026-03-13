using System.Text.Json.Serialization;

namespace ReservationSystem.Template.Api.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/template-items.
/// This model is the API contract — it is deliberately separate from the
/// application command so that the API surface can evolve independently.
/// </summary>
public sealed class CreateTemplateItemRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = [];

    [JsonPropertyName("priority")]
    public string Priority { get; init; } = "normal";

    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; init; } = [];
}
