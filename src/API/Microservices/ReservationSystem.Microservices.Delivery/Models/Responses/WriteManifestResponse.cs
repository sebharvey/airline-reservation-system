using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

public sealed class WriteManifestResponse
{
    [JsonPropertyName("written")] public int Written { get; init; }
    [JsonPropertyName("skipped")] public int Skipped { get; init; }
}
