namespace ReservationSystem.Microservices.Offer.Application.ReserveSeat;

public sealed record ReserveSeatCommand(
    Guid FlightId,
    Guid BasketId,
    List<string> SeatNumbers);
