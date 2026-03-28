namespace ReservationSystem.Microservices.User.Application.DeleteUser;

/// <summary>
/// Command to permanently delete an employee user account.
/// </summary>
public sealed record DeleteUserCommand(Guid UserId);
