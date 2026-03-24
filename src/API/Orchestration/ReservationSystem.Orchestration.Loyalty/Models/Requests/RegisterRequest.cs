namespace ReservationSystem.Orchestration.Loyalty.Models.Requests;

public sealed class RegisterRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? PhoneNumber { get; init; }
    public string? PreferredLanguage { get; init; }
}
