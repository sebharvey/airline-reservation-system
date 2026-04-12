using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

public sealed class ConfirmOrderRequest
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("basketId")]
    public Guid BasketId { get; init; }

    [JsonPropertyName("paymentReferences")]
    public List<PaymentReferenceItem>? PaymentReferences { get; init; }

    [JsonPropertyName("enrichedOffers")]
    public List<EnrichedOfferItem>? EnrichedOffers { get; init; }
}

public sealed class EnrichedOfferItem
{
    [JsonPropertyName("offerId")]
    public Guid OfferId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("baseFareAmount")]
    public decimal BaseFareAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("taxLines")]
    public List<EnrichedTaxLine>? TaxLines { get; init; }
}

public sealed class EnrichedTaxLine
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
