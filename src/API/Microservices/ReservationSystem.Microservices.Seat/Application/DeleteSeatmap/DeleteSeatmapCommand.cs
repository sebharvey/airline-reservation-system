namespace ReservationSystem.Microservices.Seat.Application.DeleteSeatmap;

/// <summary>
/// Command carrying the identifier needed to delete a seatmap definition.
/// </summary>
public sealed record DeleteSeatmapCommand(Guid SeatmapId);
