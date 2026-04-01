namespace ReservationSystem.Microservices.Ancillary.Application.Seat.GetSeatmapById;

/// <summary>
/// Query carrying the seatmap identifier needed to retrieve a single seatmap.
/// </summary>
public sealed record GetSeatmapByIdQuery(Guid SeatmapId);
