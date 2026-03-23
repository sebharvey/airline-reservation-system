namespace ReservationSystem.Microservices.Offer.Models.Responses;

public sealed class FareResponse
{
    public Guid FareId { get; init; }
    public Guid InventoryId { get; init; }
    public string FareBasisCode { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}
