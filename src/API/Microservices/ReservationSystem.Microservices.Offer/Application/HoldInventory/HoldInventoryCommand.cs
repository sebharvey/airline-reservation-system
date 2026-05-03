namespace ReservationSystem.Microservices.Offer.Application.HoldInventory;

public sealed record HoldInventoryCommand(
    Guid InventoryId,
    string CabinCode,
    int PaxCount,
    Guid OrderId,
    string HoldType = "Revenue",
    short? StandbyPriority = null);
