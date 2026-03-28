using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.User.Models.Requests;

/// <summary>
/// HTTP request body for PATCH /v1/users/{userId}.
/// All fields are optional; only supplied fields are updated.
/// </summary>
public sealed class UpdateUserRequest
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }
}
