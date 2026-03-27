using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.User.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/users.
/// </summary>
public sealed class AddUserResponse
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }
}
