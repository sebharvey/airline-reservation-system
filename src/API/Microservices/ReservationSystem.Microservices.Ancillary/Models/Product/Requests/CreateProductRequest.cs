using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Product.Requests;

public sealed class CreateProductRequest
{
    [JsonPropertyName("productGroupId")]
    public Guid ProductGroupId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("isSegmentSpecific")]
    public bool IsSegmentSpecific { get; init; }

    [JsonPropertyName("ssrCode")]
    public string? SsrCode { get; init; }

    [JsonPropertyName("imageBase64")]
    public string? ImageBase64 { get; init; }

    [JsonPropertyName("availableChannels")]
    public string AvailableChannels { get; init; } = """["WEB","APP","NDC","KIOSK","CC","AIRPORT"]""";

    [JsonPropertyName("availabilityRules")]
    public string? AvailabilityRules { get; init; }
}
