namespace ReservationSystem.Microservices.Seat.Application.UpdateSeatmap;

/// <summary>
/// Command carrying the data needed to update an existing seatmap.
/// </summary>
public sealed record UpdateSeatmapCommand(
    Guid SeatmapId,
    string CabinLayout);
