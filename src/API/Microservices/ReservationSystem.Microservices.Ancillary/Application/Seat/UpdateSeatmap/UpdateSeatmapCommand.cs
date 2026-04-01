namespace ReservationSystem.Microservices.Ancillary.Application.Seat.UpdateSeatmap;

/// <summary>
/// Command carrying the data needed to update an existing seatmap.
/// </summary>
public sealed record UpdateSeatmapCommand(
    Guid SeatmapId,
    string? CabinLayout,
    bool? IsActive);
