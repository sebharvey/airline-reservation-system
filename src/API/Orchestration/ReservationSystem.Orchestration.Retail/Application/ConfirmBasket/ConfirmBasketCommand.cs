namespace ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;

public sealed record ConfirmBasketCommand(
    Guid BasketId,
    string PaymentMethod,
    string? PaymentToken,
    decimal? LoyaltyPointsToRedeem);
