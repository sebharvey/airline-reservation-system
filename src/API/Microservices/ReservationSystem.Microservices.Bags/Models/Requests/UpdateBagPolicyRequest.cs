using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/bag-policies/{policyId}.
/// CabinCode is immutable (set at creation only) and not included here.
/// </summary>
public sealed class UpdateBagPolicyRequest
{
    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
