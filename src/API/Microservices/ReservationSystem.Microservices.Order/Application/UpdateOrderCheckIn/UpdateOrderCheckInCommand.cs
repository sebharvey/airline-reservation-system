namespace ReservationSystem.Microservices.Order.Application.UpdateOrderCheckIn;

public sealed record UpdateOrderCheckInPassenger(
    int PaxId,
    string TicketNumber,
    string Status,
    string Message)
{
    // Derived from the integer PaxId — callers must now supply paxId, not the legacy "PAX-N" string.
    public string PassengerId => $"PAX-{PaxId}";
}

public sealed record UpdateOrderCheckInNote(string DateTime, string Type, string Message, int? PaxId = null, int? SegmentId = null);

public sealed record UpdateOrderCheckInCommand(
    string BookingReference,
    string DepartureAirport,
    string CheckedInAt,
    IReadOnlyList<UpdateOrderCheckInPassenger> Passengers,
    IReadOnlyList<UpdateOrderCheckInNote>? AdditionalNotes = null);
