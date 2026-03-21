namespace ReservationSystem.Microservices.Seat.Application.CreateSeatmap;

/// <summary>
/// Command carrying the data needed to create/upload a new seatmap.
/// </summary>
public sealed record CreateSeatmapCommand(
    string AircraftTypeCode,
    string CabinLayout);
