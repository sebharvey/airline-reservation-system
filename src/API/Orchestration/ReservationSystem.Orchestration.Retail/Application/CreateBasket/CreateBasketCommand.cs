namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed record CreateBasketCommand(
    IReadOnlyList<Guid> OfferIds,
    string ChannelCode,
    string? CurrencyCode,
    string? BookingType,
    string? LoyaltyNumber,
    string? CustomerId = null);
