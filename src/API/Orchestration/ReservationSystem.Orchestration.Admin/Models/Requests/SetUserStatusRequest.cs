using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Admin.Models.Requests;

/// <summary>
/// HTTP request body for PATCH /v1/admin/users/{userId}/status.
/// </summary>
public sealed class SetUserStatusRequest
{
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
