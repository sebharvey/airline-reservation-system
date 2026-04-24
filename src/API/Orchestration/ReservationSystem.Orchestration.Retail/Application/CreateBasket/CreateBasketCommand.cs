namespace ReservationSystem.Orchestration.Retail.Application.CreateBasket;

public sealed record BasketSegment(Guid OfferId, Guid? SessionId);

public sealed record CreateBasketCommand(
    IReadOnlyList<BasketSegment> Segments,
    string? Currency,
    string? BookingType,
    string? LoyaltyNumber,
    string? CustomerId = null,
    int PassengerCount = 1,
    string? BasketType = null);
