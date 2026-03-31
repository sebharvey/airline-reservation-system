using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class CreateFareRequest
{
    [JsonPropertyName("fareBasisCode")]
    public string FareBasisCode { get; init; } = string.Empty;

    [JsonPropertyName("fareFamily")]
    public string? FareFamily { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("bookingClass")]
    public string? BookingClass { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("baseFareAmount")]
    public decimal BaseFareAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; init; }

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

    [JsonPropertyName("validFrom")]
    public string ValidFrom { get; init; } = string.Empty;

    [JsonPropertyName("validTo")]
    public string ValidTo { get; init; } = string.Empty;
}
