using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Models.Responses;

public sealed class AdminDisruptionOrdersResponse
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("orders")]
    public IReadOnlyList<IropsOrderItem> Orders { get; init; } = [];
}

public sealed class IropsOrderItem
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = "Revenue";

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("loyaltyTier")]
    public string? LoyaltyTier { get; init; }

    [JsonPropertyName("loyaltyNumber")]
    public string? LoyaltyNumber { get; init; }

    [JsonPropertyName("bookingDate")]
    public DateTime BookingDate { get; init; }

    [JsonPropertyName("passengerCount")]
    public int PassengerCount { get; init; }

    [JsonPropertyName("passengerNames")]
    public IReadOnlyList<string> PassengerNames { get; init; } = [];
}
