namespace ReservationSystem.Microservices.Seat.Application.GetSeatmapById;

/// <summary>
/// Query carrying the seatmap identifier needed to retrieve a single seatmap.
/// </summary>
public sealed record GetSeatmapByIdQuery(Guid SeatmapId);
