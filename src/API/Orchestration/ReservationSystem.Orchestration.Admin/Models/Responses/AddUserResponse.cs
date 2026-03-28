using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Responses;

/// <summary>
/// HTTP response body for POST /v1/admin/users.
/// </summary>
public sealed class AddUserResponse
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; init; }
}
