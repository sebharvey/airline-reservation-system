namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class SellInventoryRequest
{
    public IReadOnlyList<Guid> InventoryIds { get; init; } = [];
    public int PaxCount { get; init; }
    public Guid BasketId { get; init; }
}
