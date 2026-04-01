namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetAircraftType;

/// <summary>
/// Query carrying the aircraft type code needed to retrieve a single aircraft type.
/// </summary>
public sealed record GetAircraftTypeQuery(string AircraftTypeCode);
