namespace ReservationSystem.Orchestration.Loyalty.Models.Responses;

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string TokenType { get; init; } = "Bearer";
}
