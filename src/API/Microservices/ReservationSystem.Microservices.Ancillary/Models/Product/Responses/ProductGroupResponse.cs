using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Product.Responses;

public sealed class ProductGroupResponse
{
    [JsonPropertyName("productGroupId")]
    public Guid ProductGroupId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

public sealed class ProductGroupListResponse
{
    [JsonPropertyName("groups")]
    public IReadOnlyList<ProductGroupResponse> Groups { get; init; } = [];
}
