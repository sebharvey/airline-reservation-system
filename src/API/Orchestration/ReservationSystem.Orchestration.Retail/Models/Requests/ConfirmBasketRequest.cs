namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class ConfirmBasketRequest
{
    public string ChannelCode { get; init; } = string.Empty;
    public PaymentDetailsRequest Payment { get; init; } = new();
    public decimal? LoyaltyPointsToRedeem { get; init; }
}

public sealed class PaymentDetailsRequest
{
    public string Method { get; init; } = string.Empty;
    public string? CardNumber { get; init; }
    public string? ExpiryDate { get; init; }
    public string? Cvv { get; init; }
    public string? CardholderName { get; init; }
}
