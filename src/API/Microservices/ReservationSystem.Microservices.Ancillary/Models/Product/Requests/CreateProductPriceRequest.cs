using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Product.Requests;

public sealed class CreateProductPriceRequest
{
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }
}
