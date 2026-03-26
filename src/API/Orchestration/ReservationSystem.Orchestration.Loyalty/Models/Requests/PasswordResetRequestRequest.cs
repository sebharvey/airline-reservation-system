namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class PasswordResetRequestRequest
{
    public string Email { get; init; } = string.Empty;
}
