using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Order.Models.Requests;

public sealed class CreateBasketRequest
{
    [JsonPropertyName("channelCode")]
    public string ChannelCode { get; init; } = string.Empty;

    [JsonPropertyName("currency")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = "Revenue";

    [JsonPropertyName("loyaltyNumber")]
    public string? LoyaltyNumber { get; init; }

    [JsonPropertyName("totalPointsAmount")]
    public int? TotalPointsAmount { get; init; }
}
