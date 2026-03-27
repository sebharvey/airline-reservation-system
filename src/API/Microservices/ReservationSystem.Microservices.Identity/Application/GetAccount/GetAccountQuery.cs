namespace ReservationSystem.Microservices.Identity.Application.GetAccount;

/// <summary>
/// Query carrying the data needed to retrieve a user account summary by ID.
/// </summary>
public sealed record GetAccountQuery(Guid UserAccountId);
