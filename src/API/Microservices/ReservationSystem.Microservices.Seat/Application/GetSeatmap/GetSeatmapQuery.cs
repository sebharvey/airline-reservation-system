namespace ReservationSystem.Microservices.Seat.Application.GetSeatmap;

/// <summary>
/// Query carrying the aircraft type code needed to retrieve the active seatmap.
/// </summary>
public sealed record GetSeatmapQuery(string AircraftTypeCode);
