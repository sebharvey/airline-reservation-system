using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminCreateProductGroupRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; init; }
}

public sealed class AdminUpdateProductGroupRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}

public sealed class AdminCreateProductRequest
{
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
}

public sealed class AdminUpdateProductRequest
{
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

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}

public sealed class AdminCreateProductPriceRequest
{
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }
}

public sealed class AdminUpdateProductPriceRequest
{
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
