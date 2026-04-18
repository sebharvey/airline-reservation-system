using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Ancillary.Models.Seat.Responses;

/// <summary>
/// Single seat offer within the seat offers response or for single-offer validation.
/// </summary>
public sealed class SeatOfferResponse
{
    [JsonPropertyName("seatOfferId")]
    public string SeatOfferId { get; init; } = string.Empty;

    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("position")]
    public string Position { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public IReadOnlyList<string> Attributes { get; init; } = [];

    [JsonPropertyName("isSelectable")]
    public bool IsSelectable { get; init; }

    [JsonPropertyName("isChargeable")]
    public bool IsChargeable { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;
}

/// <summary>
/// Response for GET /v1/seat-offers/{seatOfferId} — validates a single seat offer.
/// </summary>
public sealed class SeatOfferValidationResponse
{
    [JsonPropertyName("seatOfferId")]
    public string SeatOfferId { get; init; } = string.Empty;

    [JsonPropertyName("flightId")]
    public Guid FlightId { get; init; }

    [JsonPropertyName("seatNumber")]
    public string SeatNumber { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("position")]
    public string Position { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public IReadOnlyList<string> Attributes { get; init; } = [];

    [JsonPropertyName("isSelectable")]
    public bool IsSelectable { get; init; }

    [JsonPropertyName("isChargeable")]
    public bool IsChargeable { get; init; }

    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    [JsonPropertyName("tax")]
    public decimal Tax { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }
}
