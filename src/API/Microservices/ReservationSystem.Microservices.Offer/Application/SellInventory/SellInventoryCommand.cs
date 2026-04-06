namespace ReservationSystem.Microservices.Offer.Application.SellInventory;

public sealed record SellInventoryCommand(
    List<SellInventoryItem> Items,
    int PaxCount,
    Guid OrderId);

public sealed record SellInventoryItem(Guid InventoryId, string CabinCode);
