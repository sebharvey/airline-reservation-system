using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.User.Models.Requests;

/// <summary>
/// HTTP request body for POST /v1/users.
/// </summary>
public sealed class AddUserRequest
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; init; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; init; } = string.Empty;
}
