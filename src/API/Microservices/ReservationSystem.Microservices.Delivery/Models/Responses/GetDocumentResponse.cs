using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class GetDocumentResponse
{
    [JsonPropertyName("documentId")] public Guid DocumentId { get; init; }
    [JsonPropertyName("documentNumber")] public string DocumentNumber { get; init; } = string.Empty;
    [JsonPropertyName("documentType")] public string DocumentType { get; init; } = string.Empty;
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("eTicketNumber")] public string? ETicketNumber { get; init; }
    [JsonPropertyName("passengerId")] public string PassengerId { get; init; } = string.Empty;
    [JsonPropertyName("segmentRef")] public string SegmentRef { get; init; } = string.Empty;
    [JsonPropertyName("paymentReference")] public string PaymentReference { get; init; } = string.Empty;
    [JsonPropertyName("amount")] public decimal Amount { get; init; }
    [JsonPropertyName("currencyCode")] public string CurrencyCode { get; init; } = string.Empty;
    [JsonPropertyName("isVoided")] public bool IsVoided { get; init; }
    [JsonPropertyName("documentData")] public JsonElement? DocumentData { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; init; }
}
