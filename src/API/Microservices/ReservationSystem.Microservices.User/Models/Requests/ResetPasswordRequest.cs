using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.User.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/users/{userId}/reset-password.
/// </summary>
public sealed class ResetPasswordRequest
{
    [JsonPropertyName("newPassword")]
    public string NewPassword { get; init; } = string.Empty;
}
