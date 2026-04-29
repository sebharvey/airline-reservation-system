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
    string? AircraftRegistration,
    string Origin,
    string Destination);

// ── Operations API operational data update ─────────────────────────────────────

public sealed record SetFlightOperationalDataRequest(string? DepartureGate, string? AircraftRegistration);

// ── Retail API admin manifest ──────────────────────────────────────────────────

public sealed record FlightManifestResponse(List<ManifestEntry> Entries);

public sealed record ManifestEntry(
    string OrderId,
    string BookingReference,
    string PassengerId,
    string GivenName,
    string Surname,
    string ETicketNumber,
    bool CheckedIn);

// ── Operations API admin check-in ─────────────────────────────────────────────

public sealed record AdminCheckInRequest(
    string DepartureAirport,
    List<CheckInPaxSubmission> Passengers,
    bool OverrideTimatic,
    string? OverrideReason);

public sealed record CheckInPaxSubmission(
    string TicketNumber,
    SimTravelDocument TravelDocument);

public sealed record SimTravelDocument(
    string Type,
    string Number,
    string IssuingCountry,
    string Nationality,
    string IssueDate,
    string ExpiryDate);
