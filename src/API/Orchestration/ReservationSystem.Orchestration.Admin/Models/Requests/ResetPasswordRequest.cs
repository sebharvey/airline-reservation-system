using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/admin/users/{userId}/reset-password.
/// </summary>
public sealed class ResetPasswordRequest
{
    [JsonPropertyName("newPassword")]
    public string NewPassword { get; init; } = string.Empty;
}
