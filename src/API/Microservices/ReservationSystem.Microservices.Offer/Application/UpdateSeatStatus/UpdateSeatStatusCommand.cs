namespace ReservationSystem.Microservices.Offer.Application.UpdateSeatStatus;

public sealed record SeatStatusUpdate(string SeatNumber, string Status);

public sealed record UpdateSeatStatusCommand(
    Guid FlightId,
    List<SeatStatusUpdate> Updates);
