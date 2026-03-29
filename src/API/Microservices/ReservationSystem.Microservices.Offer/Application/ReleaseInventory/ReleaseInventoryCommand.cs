namespace ReservationSystem.Microservices.Offer.Application.ReleaseInventory;

public sealed record ReleaseInventoryCommand(
    Guid InventoryId,
    string CabinCode,
    int PaxCount,
    string ReleaseType,
    Guid? BasketId);
