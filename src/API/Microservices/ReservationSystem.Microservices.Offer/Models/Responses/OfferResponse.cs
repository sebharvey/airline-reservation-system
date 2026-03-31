using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Responses;

/// <summary>
/// A single cabin fare offer within a flight search result, identified by its own OfferId.
/// </summary>
public sealed class CabinOfferItem
{
    [JsonPropertyName("offerId")]
    public Guid OfferId { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("fareBasisCode")]
    public string FareBasisCode { get; init; } = string.Empty;

    [JsonPropertyName("fareFamily")]
    public string? FareFamily { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("baseFareAmount")]
    public decimal BaseFareAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("isRefundable")]
    public bool IsRefundable { get; init; }

    [JsonPropertyName("isChangeable")]
    public bool IsChangeable { get; init; }

    [JsonPropertyName("changeFeeAmount")]
    public decimal ChangeFeeAmount { get; init; }

    [JsonPropertyName("cancellationFeeAmount")]
    public decimal CancellationFeeAmount { get; init; }

    [JsonPropertyName("pointsPrice")]
    public int? PointsPrice { get; init; }

    [JsonPropertyName("pointsTaxes")]
    public decimal? PointsTaxes { get; init; }

    [JsonPropertyName("seatsAvailable")]
    public int SeatsAvailable { get; init; }

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = string.Empty;
}

/// <summary>
/// A flight in the search response. Flight details come from FlightInventory;
/// fare offers come from FaresInfo.
/// </summary>
public sealed class FlightOfferItem
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("arrivalDayOffset")]
    public int ArrivalDayOffset { get; init; }

    [JsonPropertyName("aircraftType")]
    public string AircraftType { get; init; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; init; } = string.Empty;

    [JsonPropertyName("offers")]
    public IReadOnlyList<CabinOfferItem> Offers { get; init; } = [];
}

public sealed class SearchOffersResponse
{
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; init; }

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("flights")]
    public IReadOnlyList<FlightOfferItem> Flights { get; init; } = [];
}

public sealed class StoredOfferResponse
{
    [JsonPropertyName("storedOfferId")]
    public Guid StoredOfferId { get; init; }

    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; init; }

    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; init; } = string.Empty;

    [JsonPropertyName("faresInfo")]
    public StoredOfferFaresInfoResponse FaresInfo { get; init; } = new();
}

public sealed class StoredOfferFaresInfoResponse
{
    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("offers")]
    public IReadOnlyList<CabinOfferItem> Offers { get; init; } = [];
}
