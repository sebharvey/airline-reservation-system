namespace ReservationSystem.Microservices.Identity.Application.GetAccountByEmail;

/// <summary>
/// Query carrying the data needed to retrieve a user account by email address.
/// </summary>
public sealed record GetAccountByEmailQuery(string Email);
