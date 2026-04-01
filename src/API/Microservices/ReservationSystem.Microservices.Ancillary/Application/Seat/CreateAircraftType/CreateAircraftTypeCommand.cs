namespace ReservationSystem.Microservices.Ancillary.Application.Seat.CreateAircraftType;

/// <summary>
/// Command carrying the data needed to create a new aircraft type.
/// </summary>
public sealed record CreateAircraftTypeCommand(
    string AircraftTypeCode,
    string Manufacturer,
    int TotalSeats,
    string? FriendlyName,
    string? CabinCounts);
