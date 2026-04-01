namespace ReservationSystem.Microservices.Ancillary.Application.Seat.DeleteSeatmap;

/// <summary>
/// Command carrying the identifier needed to delete a seatmap definition.
/// </summary>
public sealed record DeleteSeatmapCommand(Guid SeatmapId);
