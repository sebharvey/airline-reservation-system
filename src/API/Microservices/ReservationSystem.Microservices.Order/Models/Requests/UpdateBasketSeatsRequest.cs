using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/basket/{basketId}/seats.
/// </summary>
public sealed class UpdateBasketSeatsRequest
{
    [JsonPropertyName("seatsData")]
    public string SeatsData { get; init; } = string.Empty;
}
