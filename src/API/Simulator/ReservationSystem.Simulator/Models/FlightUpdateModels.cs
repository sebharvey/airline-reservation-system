namespace ReservationSystem.Simulator.Models;

// ── Admin API login ────────────────────────────────────────────────────────────

public sealed record AdminLoginRequest(string Username, string Password);

public sealed record AdminLoginResponse(string AccessToken, string UserId, string ExpiresAt, string TokenType);

// ── Retail API admin inventory ─────────────────────────────────────────────────

public sealed record FlightInventoryItem(
    Guid InventoryId,
    string FlightNumber,
    string DepartureDate,
    string DepartureTime,
    string Status,
    string? DepartureGate,
    string? AircraftRegistration);

// ── Operations API operational data update ─────────────────────────────────────

public sealed record SetFlightOperationalDataRequest(string? DepartureGate, string? AircraftRegistration);
