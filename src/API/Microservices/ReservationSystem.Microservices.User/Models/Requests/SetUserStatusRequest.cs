using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.User.Models.Requests;

/// <summary>
/// HTTP request body for PATCH /v1/users/{userId}/status.
/// </summary>
public sealed class SetUserStatusRequest
{
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
