namespace ReservationSystem.Microservices.Order.Application.UpdateOrderCheckIn;

public sealed record UpdateOrderCheckInPassenger(
    string PassengerId,
    string TicketNumber,
    string Status,
    string Message);

public sealed record UpdateOrderCheckInNote(string DateTime, string Type, string Message, int? PaxId = null);

public sealed record UpdateOrderCheckInCommand(
    string BookingReference,
    string DepartureAirport,
    string CheckedInAt,
    IReadOnlyList<UpdateOrderCheckInPassenger> Passengers,
    IReadOnlyList<UpdateOrderCheckInNote>? AdditionalNotes = null);
