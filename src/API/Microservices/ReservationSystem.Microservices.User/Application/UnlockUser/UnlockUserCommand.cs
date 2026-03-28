namespace ReservationSystem.Microservices.User.Application.UnlockUser;

/// <summary>
/// Command to unlock a locked employee user account.
/// </summary>
public sealed record UnlockUserCommand(Guid UserId);
