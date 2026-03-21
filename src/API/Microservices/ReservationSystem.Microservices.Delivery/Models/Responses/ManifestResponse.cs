using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

/// <summary>
/// HTTP response body for Manifest endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class ManifestResponse
{
    [JsonPropertyName("manifestId")]
    public Guid ManifestId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("manifestStatus")]
    public string ManifestStatus { get; init; } = string.Empty;

    [JsonPropertyName("manifestData")]
    public string ManifestData { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}
