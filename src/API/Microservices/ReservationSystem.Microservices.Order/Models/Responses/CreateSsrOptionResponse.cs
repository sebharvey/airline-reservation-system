using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class CreateSsrOptionResponse
{
    [JsonPropertyName("ssrCatalogueId")]
    public Guid SsrCatalogueId { get; init; }

    [JsonPropertyName("ssrCode")]
    public string SsrCode { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
