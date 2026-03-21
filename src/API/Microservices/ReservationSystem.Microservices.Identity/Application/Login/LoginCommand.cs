namespace ReservationSystem.Microservices.Identity.Application.Login;

/// <summary>
/// Command carrying the credentials needed to authenticate a user.
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password);
