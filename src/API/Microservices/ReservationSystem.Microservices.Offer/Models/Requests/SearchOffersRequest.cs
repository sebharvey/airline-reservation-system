using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class SearchOffersRequest
{
    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("paxCount")]
    public int PaxCount { get; init; }

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = "Revenue";

    [JsonPropertyName("includePrivateFares")]
    public bool IncludePrivateFares { get; init; }
}
