namespace ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;

public sealed record UpdateProfileCommand(
    string LoyaltyNumber,
    string? GivenName,
    string? Surname,
    DateOnly? DateOfBirth,
    string? Nationality,
    string? PhoneNumber,
    string? PreferredLanguage);
