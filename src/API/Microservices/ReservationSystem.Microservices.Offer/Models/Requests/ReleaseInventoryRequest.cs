namespace ReservationSystem.Microservices.Offer.Models.Requests;

public sealed class ReleaseInventoryRequest
{
    public Guid InventoryId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public int PaxCount { get; init; }
    public string ReleaseType { get; init; } = string.Empty;
    public Guid? BasketId { get; init; }
}
