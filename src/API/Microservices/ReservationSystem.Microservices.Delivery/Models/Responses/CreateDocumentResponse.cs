using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class CreateDocumentResponse
{
    [JsonPropertyName("documentId")] public Guid DocumentId { get; init; }
    [JsonPropertyName("documentNumber")] public string DocumentNumber { get; init; } = string.Empty;
    [JsonPropertyName("documentType")] public string DocumentType { get; init; } = string.Empty;
    [JsonPropertyName("bookingReference")] public string BookingReference { get; init; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
}
