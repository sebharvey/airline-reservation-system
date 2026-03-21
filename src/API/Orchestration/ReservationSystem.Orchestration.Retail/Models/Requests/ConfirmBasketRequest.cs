namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class ConfirmBasketRequest
{
    public string PaymentMethod { get; init; } = string.Empty;
    public string? PaymentToken { get; init; }
    public decimal? LoyaltyPointsToRedeem { get; init; }
}
