using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Responses;

/// <summary>
/// Response for GET /v1/bags/offers — returns free bag policy and purchasable additional bag offers.
/// </summary>
public sealed class BagOffersResponse
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("policy")]
    public BagPolicyInfo Policy { get; init; } = new();

    [JsonPropertyName("bagOffers")]
    public IReadOnlyList<BagOfferItem> BagOffers { get; init; } = [];
}

public sealed class BagPolicyInfo
{
    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }
}

public sealed class BagOfferItem
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
/// Response for GET /v1/bags/offers/{bagOfferId} — validates a single bag offer.
/// </summary>
public sealed class BagOfferValidationResponse
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
