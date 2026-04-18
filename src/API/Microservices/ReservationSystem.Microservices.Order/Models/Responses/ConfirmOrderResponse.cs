using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Responses;

public sealed class ConfirmOrderResponse
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("totalAmount")]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("currency")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("orderItems")]
    public IReadOnlyList<ConfirmedOrderItem> OrderItems { get; init; } = [];
}

public sealed class ConfirmedOrderItem
{
    [JsonPropertyName("offerId")]
    public Guid OfferId { get; init; }

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

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("fareFamily")]
    public string? FareFamily { get; init; }

    [JsonPropertyName("fareBasisCode")]
    public string? FareBasisCode { get; init; }

    [JsonPropertyName("baseFareAmount")]
    public decimal BaseFareAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("taxLines")]
    public IReadOnlyList<ConfirmedTaxLine>? TaxLines { get; init; }

    [JsonPropertyName("productType")]
    public string? ProductType { get; init; }
}

public sealed class ConfirmedTaxLine
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
