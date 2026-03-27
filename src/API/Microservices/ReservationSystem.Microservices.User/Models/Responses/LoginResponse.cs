using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.User.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/users/login.
/// </summary>
public sealed class LoginResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; init; }
}
