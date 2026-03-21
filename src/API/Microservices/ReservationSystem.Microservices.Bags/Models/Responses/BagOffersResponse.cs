using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Responses;

/// <summary>
/// HTTP response body for GET /v1/bags/offers (list bag offers).
/// </summary>
public sealed class BagOffersResponse
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("policy")]
    public BagOfferPolicyResponse Policy { get; init; } = new();

    [JsonPropertyName("bagOffers")]
    public IReadOnlyList<BagOfferItemResponse> BagOffers { get; init; } = [];
}

/// <summary>
/// Nested policy information within a bag offers response.
/// </summary>
public sealed class BagOfferPolicyResponse
{
    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }
}

/// <summary>
/// Represents a single bag purchase option within the bag offers list.
/// </summary>
public sealed class BagOfferItemResponse
{
    [JsonPropertyName("bagOfferId")]
    public string BagOfferId { get; init; } = string.Empty;

    [JsonPropertyName("bagSequence")]
    public int BagSequence { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}

/// <summary>
/// HTTP response body for GET /v1/bags/offers/{bagOfferId} (single bag offer validation).
/// </summary>
public sealed class BagOfferResponse
{
    [JsonPropertyName("bagOfferId")]
    public string BagOfferId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("bagSequence")]
    public int BagSequence { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }
}
