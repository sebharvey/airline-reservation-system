namespace ReservationSystem.Microservices.Identity.Application.RefreshToken;

/// <summary>
/// Command carrying the data needed to refresh an access token.
/// </summary>
public sealed record RefreshTokenCommand(
    string Token,
    string? DeviceHint);
