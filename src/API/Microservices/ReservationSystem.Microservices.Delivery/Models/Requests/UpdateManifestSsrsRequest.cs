using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Requests;

public sealed class UpdateManifestSsrsRequest
{
    [JsonPropertyName("entries")] public List<ManifestSsrEntryRequest> Entries { get; init; } = [];
}

public sealed class ManifestSsrEntryRequest
{
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("ssrCodes")]      public List<string> SsrCodes { get; init; } = [];
}
