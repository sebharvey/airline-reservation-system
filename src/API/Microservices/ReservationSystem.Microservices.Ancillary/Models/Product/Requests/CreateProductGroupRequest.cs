using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Product.Requests;

public sealed class CreateProductGroupRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
