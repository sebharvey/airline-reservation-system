namespace ReservationSystem.Microservices.Identity.Application.CreateAccount;

/// <summary>
/// Command carrying the data needed to register a new user account.
/// </summary>
public sealed record CreateAccountCommand(
    string Email,
    string Password);
