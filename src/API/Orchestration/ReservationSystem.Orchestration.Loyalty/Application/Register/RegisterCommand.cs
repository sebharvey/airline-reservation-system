namespace ReservationSystem.Orchestration.Loyalty.Application.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string GivenName,
    string Surname,
    DateOnly? DateOfBirth,
    string? PhoneNumber,
    string? PreferredLanguage);
