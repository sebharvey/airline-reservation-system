namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class SellInventoryRequest
{
    public IReadOnlyList<SellInventoryItemRequest> Items { get; init; } = [];
    public int PaxCount { get; init; }
    public Guid BasketId { get; init; }
}

public sealed class SellInventoryItemRequest
{
    public Guid InventoryId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
}
