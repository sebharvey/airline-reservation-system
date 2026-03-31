using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class CreateFareRuleRequest
{
    [JsonPropertyName("ruleType")]
    public string RuleType { get; init; } = "Money";

    [JsonPropertyName("flightNumber")]
    public string? FlightNumber { get; init; }

    [JsonPropertyName("fareBasisCode")]
    public string FareBasisCode { get; init; } = string.Empty;

    [JsonPropertyName("fareFamily")]
    public string? FareFamily { get; init; }

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("bookingClass")]
    public string BookingClass { get; init; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public string? CurrencyCode { get; init; }

    [JsonPropertyName("minAmount")]
    public decimal? MinAmount { get; init; }

    [JsonPropertyName("maxAmount")]
    public decimal? MaxAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal? TaxAmount { get; init; }

    [JsonPropertyName("minPoints")]
    public int? MinPoints { get; init; }

    [JsonPropertyName("maxPoints")]
    public int? MaxPoints { get; init; }

    [JsonPropertyName("pointsTaxes")]
    public decimal? PointsTaxes { get; init; }

    [JsonPropertyName("isRefundable")]
    public bool IsRefundable { get; init; }

    [JsonPropertyName("isChangeable")]
    public bool IsChangeable { get; init; }

    [JsonPropertyName("changeFeeAmount")]
    public decimal ChangeFeeAmount { get; init; }

    [JsonPropertyName("cancellationFeeAmount")]
    public decimal CancellationFeeAmount { get; init; }

    [JsonPropertyName("validFrom")]
    public string? ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public string? ValidTo { get; init; }
}
