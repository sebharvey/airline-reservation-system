using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class CreateDocumentRequest
{
    [JsonPropertyName("documentType")] public string DocumentType { get; init; } = string.Empty;
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("segmentRef")] public string SegmentRef { get; init; } = string.Empty;
    [JsonPropertyName("paymentReference")] public string PaymentReference { get; init; } = string.Empty;
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("currencyCode")] public string CurrencyCode { get; init; } = string.Empty;
    [JsonPropertyName("documentData")] public JsonElement? DocumentData { get; init; }
}
