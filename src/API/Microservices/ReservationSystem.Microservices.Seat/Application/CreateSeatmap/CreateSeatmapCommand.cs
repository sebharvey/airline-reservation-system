namespace ReservationSystem.Microservices.Seat.Application.CreateSeatmap;

public sealed record CreateSeatmapCommand(
    string AircraftTypeCode,
    string CabinLayout);
