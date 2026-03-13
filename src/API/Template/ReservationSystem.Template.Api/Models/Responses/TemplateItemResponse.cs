using System.Text.Json.Serialization;

namespace ReservationSystem.Template.Api.Models.Responses;

/// <summary>
/// HTTP response body for TemplateItem endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class TemplateItemResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("metadata")]
    public TemplateItemMetadataResponse Metadata { get; init; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class TemplateItemMetadataResponse
{
    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = [];

    [JsonPropertyName("priority")]
    public string Priority { get; init; } = string.Empty;

    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; init; } = [];
}
