namespace ReservationSystem.Orchestration.Retail.Application.OciCheckIn;

public sealed record OciCheckInCommand(
    string BookingReference,
    List<OciCheckInPassengerCommand> Passengers);

public sealed record OciCheckInPassengerCommand(
    string PassengerId,
    List<string> InventoryIds);
