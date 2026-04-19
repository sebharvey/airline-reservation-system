using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Payment.Models.Requests;

/// <summary>
/// HTTP request body for PATCH /v1/payment/{paymentId}/booking-reference.
/// </summary>
public sealed class UpdateBookingReferenceRequest
{
    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;
}
