using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/documents.
/// </summary>
public sealed class CreateDocumentRequest
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; init; } = string.Empty;

    [JsonPropertyName("documentData")]
    public string DocumentData { get; init; } = "{}";
}
