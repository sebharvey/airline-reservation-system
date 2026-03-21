namespace ReservationSystem.Microservices.Offer.Application.HoldInventory;

public sealed record HoldInventoryCommand(
    Guid InventoryId,
    string CabinCode,
    int PaxCount,
    Guid BasketId);
