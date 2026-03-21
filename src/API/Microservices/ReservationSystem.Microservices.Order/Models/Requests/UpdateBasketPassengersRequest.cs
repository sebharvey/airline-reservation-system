using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

/// <summary>
/// HTTP request body for PUT /v1/basket/{basketId}/passengers.
/// </summary>
public sealed class UpdateBasketPassengersRequest
{
    [JsonPropertyName("passengersData")]
    public string PassengersData { get; init; } = string.Empty;
}
