namespace ReservationSystem.Microservices.Offer.Application.HoldInventory;

public sealed record PaxHold(string? SeatNumber, string? PassengerId);

public sealed record HoldInventoryCommand(
    Guid InventoryId,
    string CabinCode,
    IReadOnlyList<PaxHold> Passengers,
    Guid OrderId,
    string HoldType = "Revenue",
    short? StandbyPriority = null);
