using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Product.Responses;

public sealed class ProductResponse
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; init; }

    [JsonPropertyName("productGroupId")]
    public Guid ProductGroupId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("isSegmentSpecific")]
    public bool IsSegmentSpecific { get; init; }

    [JsonPropertyName("ssrCode")]
    public string? SsrCode { get; init; }

    [JsonPropertyName("imageBase64")]
    public string? ImageBase64 { get; init; }

    [JsonPropertyName("availableChannels")]
    public string AvailableChannels { get; init; } = """["WEB","APP","NDC","GDS","KIOSK","CC","AIRPORT"]""";

    [JsonPropertyName("availabilityRules")]
    public string? AvailabilityRules { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("prices")]
    public IReadOnlyList<ProductPriceResponse> Prices { get; init; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

public sealed class ProductListResponse
{
    [JsonPropertyName("products")]
    public IReadOnlyList<ProductResponse> Products { get; init; } = [];
}
