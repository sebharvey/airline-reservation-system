using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class BagOfferValidationDto
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

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }
}

public sealed class BagPolicyDto
{
    public Guid PolicyId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public int FreeBagsIncluded { get; init; }
    public int MaxWeightKgPerBag { get; init; }
    public bool IsActive { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class BagPoliciesListDto
{
    public IReadOnlyList<BagPolicyDto> Policies { get; init; } = [];
}

public sealed class BagPricingDto
{
    public Guid PricingId { get; init; }
    public int BagSequence { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public string ValidFrom { get; init; } = string.Empty;
    public string? ValidTo { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class BagPricingListDto
{
    public IReadOnlyList<BagPricingDto> Pricing { get; init; } = [];
}
