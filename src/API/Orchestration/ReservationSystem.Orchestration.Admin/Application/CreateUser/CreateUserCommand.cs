namespace ReservationSystem.Orchestration.Admin.Application.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName);
