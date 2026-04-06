namespace ReservationSystem.Microservices.Offer.Application.HoldInventory;

public sealed record HoldInventoryCommand(
    Guid InventoryId,
    string CabinCode,
    IReadOnlyList<string?> Passengers,
    Guid OrderId);
