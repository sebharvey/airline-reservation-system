namespace ReservationSystem.Microservices.User.Application.SetUserStatus;

/// <summary>
/// Command to activate or deactivate an employee user account.
/// </summary>
public sealed record SetUserStatusCommand(Guid UserId, bool IsActive);
