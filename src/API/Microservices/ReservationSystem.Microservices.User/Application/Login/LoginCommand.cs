namespace ReservationSystem.Microservices.User.Application.Login;

/// <summary>
/// Command to authenticate an employee user with username and password.
/// </summary>
public sealed record LoginCommand(string Username, string Password);
