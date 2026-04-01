using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Bag.Requests;

public sealed class UpdateBagPolicyRequest
{
    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
