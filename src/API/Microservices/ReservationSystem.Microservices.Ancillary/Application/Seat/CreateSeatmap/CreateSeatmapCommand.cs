namespace ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatmap;

public sealed record CreateSeatmapCommand(
    string AircraftTypeCode,
    string CabinLayout);
