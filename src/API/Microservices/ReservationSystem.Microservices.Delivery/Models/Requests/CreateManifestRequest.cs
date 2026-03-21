using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/manifests.
/// </summary>
public sealed class CreateManifestRequest
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("manifestData")]
    public string ManifestData { get; init; } = "{}";
}
