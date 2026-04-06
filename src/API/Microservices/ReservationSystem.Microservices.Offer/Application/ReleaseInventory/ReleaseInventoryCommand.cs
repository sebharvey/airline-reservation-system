namespace ReservationSystem.Microservices.Offer.Application.ReleaseInventory;

public sealed record ReleaseInventoryCommand(
    Guid InventoryId,
    string CabinCode,
    Guid OrderId,
    string ReleaseType,
    Guid? BasketId);
