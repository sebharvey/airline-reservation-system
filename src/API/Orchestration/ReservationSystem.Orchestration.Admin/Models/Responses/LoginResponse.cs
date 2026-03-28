using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/auth/login.
/// </summary>
public sealed class LoginResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; init; }

    [JsonPropertyName("tokenType")]
    public string TokenType { get; init; } = "Bearer";
}
