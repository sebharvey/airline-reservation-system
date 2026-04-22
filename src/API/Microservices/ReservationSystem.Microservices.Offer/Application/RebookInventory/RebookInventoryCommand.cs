using ReservationSystem.Microservices.Offer.Application.SellInventory;

namespace ReservationSystem.Microservices.Offer.Application.RebookInventory;

public sealed record RebookInventoryCommand(
    Guid FromInventoryId,
    string FromCabinCode,
    IReadOnlyList<SellInventoryItem> ToItems,
    Guid OrderId);
