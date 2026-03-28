using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/auth/login.
/// </summary>
public sealed class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}
