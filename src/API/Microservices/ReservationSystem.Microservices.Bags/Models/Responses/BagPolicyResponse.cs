using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Responses;

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
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}
