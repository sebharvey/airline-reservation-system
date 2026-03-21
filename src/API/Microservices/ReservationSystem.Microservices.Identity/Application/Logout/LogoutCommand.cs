namespace ReservationSystem.Microservices.Identity.Application.Logout;

/// <summary>
/// Command carrying the data needed to log a user out and revoke their refresh token.
/// </summary>
public sealed record LogoutCommand(
    string RefreshToken);
