using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/authorise.
/// Matches the API specification contract with nested cardDetails object.
/// </summary>
public sealed class AuthorisePaymentRequest
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("cardDetails")]
    public CardDetailsRequest? CardDetails { get; init; }

    [JsonPropertyName("paymentType")]
    public string PaymentType { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }
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
