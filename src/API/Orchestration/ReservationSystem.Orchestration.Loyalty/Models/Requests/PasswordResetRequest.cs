namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class PasswordResetRequest
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
