using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

/// <summary>
/// HTTP request body for PATCH /v1/manifests/{manifestId}.
/// </summary>
public sealed class UpdateManifestRequest
{
    [JsonPropertyName("manifestData")]
    public string ManifestData { get; init; } = string.Empty;
}
