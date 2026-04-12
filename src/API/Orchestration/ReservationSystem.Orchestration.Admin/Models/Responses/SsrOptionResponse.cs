using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Responses;

public sealed class SsrOptionResponse
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

public sealed class SsrOptionListResponse
{
    [JsonPropertyName("ssrOptions")]
    public IReadOnlyList<SsrOptionSummary> SsrOptions { get; init; } = [];
}

public sealed class SsrOptionSummary
{
    [JsonPropertyName("ssrCode")]
    public string SsrCode { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;
}
