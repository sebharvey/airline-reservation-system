using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/bag-policies/{policyId}.
/// </summary>
public sealed class UpdateBagPolicyRequest
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
