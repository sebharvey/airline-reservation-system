using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class SearchFareRulesRequest
{
    [JsonPropertyName("query")]
    public string? Query { get; init; }
}
