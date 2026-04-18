using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Responses;

/// <summary>
/// Derived value for a single flight coupon. All fields are computed from the ticket's
/// fare construction and tax breakdown; none are stored as authoritative amounts.
/// </summary>
public sealed class GetCouponValueResponse
{
    [JsonPropertyName("eTicketNumber")] public string ETicketNumber { get; init; } = string.Empty;
    [JsonPropertyName("couponNumber")] public int CouponNumber { get; init; }
    [JsonPropertyName("fareShare")] public decimal FareShare { get; init; }
    [JsonPropertyName("taxShare")] public decimal TaxShare { get; init; }
    [JsonPropertyName("total")] public decimal Total { get; init; }
    [JsonPropertyName("currency")] public string Currency { get; init; } = string.Empty;

    /// <summary>Always "derived" — indicates this value is computed, not stored.</summary>
    [JsonPropertyName("valueSource")] public string ValueSource { get; init; } = "derived";
}
