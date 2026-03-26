namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class VerifyEmailChangeRequest
{
    public string Token { get; init; } = string.Empty;
    public string NewEmail { get; init; } = string.Empty;
}
