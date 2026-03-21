namespace ReservationSystem.Microservices.Offer.Application.ReleaseInventory;

public sealed record ReleaseInventoryCommand(
    Guid InventoryId,
    int PaxCount,
    string ReleaseType,
    Guid? BasketId);
