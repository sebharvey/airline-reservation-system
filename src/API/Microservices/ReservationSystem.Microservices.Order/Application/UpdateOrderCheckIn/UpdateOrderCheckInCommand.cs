namespace ReservationSystem.Microservices.Order.Application.UpdateOrderCheckIn;

public sealed record UpdateOrderCheckInPassenger(
    string PassengerId,
    string TicketNumber,
    string Status,
    string Message);

public sealed record UpdateOrderCheckInTimaticNote(string Message);

public sealed record UpdateOrderCheckInCommand(
    string BookingReference,
    string DepartureAirport,
    string CheckedInAt,
    IReadOnlyList<UpdateOrderCheckInPassenger> Passengers,
    IReadOnlyList<UpdateOrderCheckInTimaticNote>? TimaticNotes = null);
