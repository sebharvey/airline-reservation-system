using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.OfferApi.Models.Responses;

/// <summary>
/// HTTP response body for Offer endpoints.
/// Flat, serialisation-ready — no domain types leak through.
/// </summary>
public sealed class OfferResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

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

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("metadata")]
    public OfferMetadataResponse Metadata { get; init; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class OfferMetadataResponse
{
    [JsonPropertyName("baggageAllowance")]
    public string BaggageAllowance { get; init; } = string.Empty;

    [JsonPropertyName("isRefundable")]
    public bool IsRefundable { get; init; }

    [JsonPropertyName("isChangeable")]
    public bool IsChangeable { get; init; }

    [JsonPropertyName("seatsRemaining")]
    public int SeatsRemaining { get; init; }
}
