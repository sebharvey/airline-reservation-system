using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class OciBoardingPassesRequest
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;
}
