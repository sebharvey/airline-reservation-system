namespace ReservationSystem.Microservices.Seat.Application.UpdateAircraftType;

/// <summary>
/// Command carrying the data needed to update an existing aircraft type.
/// </summary>
public sealed record UpdateAircraftTypeCommand(
    string AircraftTypeCode,
    string? Manufacturer,
    int? TotalSeats,
    string? FriendlyName,
    bool? IsActive);
