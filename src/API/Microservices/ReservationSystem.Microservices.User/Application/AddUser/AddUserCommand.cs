namespace ReservationSystem.Microservices.User.Application.AddUser;

/// <summary>
/// Command to create a new Apex Air employee user account.
/// </summary>
public sealed record AddUserCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName);
