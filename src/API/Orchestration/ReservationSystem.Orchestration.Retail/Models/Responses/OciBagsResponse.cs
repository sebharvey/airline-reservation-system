namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class OciBagsResponse
{
    public string BookingReference { get; init; } = string.Empty;
    public int BagsPurchased { get; init; }
    public string? PaymentReference { get; init; }
}
