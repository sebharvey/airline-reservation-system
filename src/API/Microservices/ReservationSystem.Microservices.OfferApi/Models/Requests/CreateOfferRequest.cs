using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.OfferApi.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/offers.
/// This model is the API contract — it is deliberately separate from the
/// application command so that the API surface can evolve independently.
/// </summary>
public sealed class CreateOfferRequest
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureAt")]
    public DateTimeOffset DepartureAt { get; init; }

    [JsonPropertyName("fareClass")]
    public string FareClass { get; init; } = string.Empty;

    [JsonPropertyName("totalPrice")]
    public decimal TotalPrice { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("baggageAllowance")]
    public string BaggageAllowance { get; init; } = string.Empty;

    [JsonPropertyName("isRefundable")]
    public bool IsRefundable { get; init; }

    [JsonPropertyName("isChangeable")]
    public bool IsChangeable { get; init; }

    [JsonPropertyName("seatsRemaining")]
    public int SeatsRemaining { get; init; }
}
