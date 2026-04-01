namespace ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteAircraftType;

/// <summary>
/// Command carrying the identifier needed to delete an aircraft type.
/// </summary>
public sealed record DeleteAircraftTypeCommand(string AircraftTypeCode);
