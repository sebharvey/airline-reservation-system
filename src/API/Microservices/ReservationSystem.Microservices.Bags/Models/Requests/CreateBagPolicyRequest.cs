using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Bags.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/bag-policies.
/// </summary>
public sealed class CreateBagPolicyRequest
{
    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("freeBagsIncluded")]
    public int FreeBagsIncluded { get; init; }

    [JsonPropertyName("maxWeightKgPerBag")]
    public int MaxWeightKgPerBag { get; init; }
}
