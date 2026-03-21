using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Responses;

/// <summary>
/// HTTP response body for bag offer generation endpoints.
/// Aggregates bag policy and pricing information for a given inventory.
/// </summary>
public sealed class BagOffersResponse
{
    [JsonPropertyName("bagOfferId")]
    public string BagOfferId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("additionalBagOptions")]
    public IReadOnlyList<BagOptionResponse> AdditionalBagOptions { get; init; } = [];
}

/// <summary>
/// Represents a single additional bag purchase option within a bag offer.
/// </summary>
public sealed class BagOptionResponse
{
    [JsonPropertyName("bagNumber")]
    public int BagNumber { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;
}
