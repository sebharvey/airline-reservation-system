namespace ReservationSystem.Simulator.Models;

public sealed record AdminLoginRequest(string Username, string Password);

public sealed record AdminLoginResponse(
    string AccessToken,
    string UserId,
    string ExpiresAt,
    string TokenType);
