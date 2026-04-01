namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatmap;

/// <summary>
/// Query carrying the aircraft type code needed to retrieve the active seatmap.
/// </summary>
public sealed record GetSeatmapQuery(string AircraftTypeCode);
