using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/{paymentId}/authorise.
/// Supports partial authorisation: when <see cref="Amount"/> is provided, only that
/// portion of the initialised total is authorised. Omitting it authorises the full
/// remaining uninitialised amount, preserving backwards compatibility.
/// </summary>
public sealed class AuthorisePaymentRequest
{
    /// <summary>
    /// Identifies the product being paid for (e.g. Fare, Seat, Bag, Product).
    /// Recorded on the resulting PaymentEvent row. Required.
    /// </summary>
    [JsonPropertyName("productType")]
    public string ProductType { get; init; } = string.Empty;

    /// <summary>
    /// Amount to authorise. Optional — when omitted the full remaining uninitialised
    /// amount is authorised. Must be greater than zero when provided.
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("cardDetails")]
    public CardDetailsRequest? CardDetails { get; init; }
}

/// <summary>
/// Card details provided during authorisation.
/// Full cardNumber and cvv are held in memory only — never persisted (PCI DSS).
/// </summary>
public sealed class CardDetailsRequest
{
    [JsonPropertyName("cardNumber")]
    public string CardNumber { get; init; } = string.Empty;

    [JsonPropertyName("expiryDate")]
    public string ExpiryDate { get; init; } = string.Empty;

    [JsonPropertyName("cvv")]
    public string Cvv { get; init; } = string.Empty;

    [JsonPropertyName("cardholderName")]
    public string CardholderName { get; init; } = string.Empty;
}
