using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/payment/{paymentId}/authorise.
/// Card details only — amount and order context are already set at initialisation.
/// </summary>
public sealed class AuthorisePaymentRequest
{
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
