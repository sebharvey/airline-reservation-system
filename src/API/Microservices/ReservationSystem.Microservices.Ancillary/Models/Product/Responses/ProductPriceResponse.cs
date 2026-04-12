using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Product.Responses;

public sealed class ProductPriceResponse
{
    [JsonPropertyName("priceId")]
    public Guid PriceId { get; init; }

    [JsonPropertyName("productId")]
    public Guid ProductId { get; init; }

    [JsonPropertyName("offerId")]
    public Guid OfferId { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}
