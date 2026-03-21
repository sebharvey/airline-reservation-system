namespace ReservationSystem.Orchestration.Loyalty.Application.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? PhoneNumber);
