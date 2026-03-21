using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/basket/{basketId}/flights.
/// </summary>
public sealed class UpdateBasketFlightsRequest
{
    [JsonPropertyName("flightsData")]
    public string FlightsData { get; init; } = string.Empty;
}
