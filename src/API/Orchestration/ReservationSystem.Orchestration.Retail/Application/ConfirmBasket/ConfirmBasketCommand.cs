namespace ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;

public sealed record ConfirmBasketCommand(
    Guid BasketId,
    string PaymentMethod,
    string? CardNumber,
    string? ExpiryDate,
    string? Cvv,
    string? CardholderName,
    decimal? LoyaltyPointsToRedeem);
