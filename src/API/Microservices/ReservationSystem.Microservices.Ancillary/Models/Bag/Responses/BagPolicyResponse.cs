using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Bag.Responses;

/// <summary>
/// HTTP response body for BagPolicy endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class BagPolicyResponse
{
    [JsonPropertyName("policyId")]
    public Guid PolicyId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Wrapper response for GET /v1/bag-policies list endpoint.
/// </summary>
public sealed class BagPoliciesListResponse
{
    [JsonPropertyName("policies")]
    public IReadOnlyList<BagPolicyResponse> Policies { get; init; } = [];
}
