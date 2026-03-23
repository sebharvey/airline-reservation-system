namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class HoldInventoryRequest
{
    public Guid InventoryId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public int PaxCount { get; init; }
    public Guid BasketId { get; init; }
}
