namespace ReservationSystem.Microservices.Offer.Application.SellInventory;

public sealed record SellInventoryCommand(
    List<Guid> InventoryIds,
    int PaxCount,
    Guid BasketId);
