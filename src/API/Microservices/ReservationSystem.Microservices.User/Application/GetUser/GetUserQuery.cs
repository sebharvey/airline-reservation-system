namespace ReservationSystem.Microservices.User.Application.GetUser;

/// <summary>
/// Query to retrieve a single employee user account by ID.
/// </summary>
public sealed record GetUserQuery(Guid UserId);
