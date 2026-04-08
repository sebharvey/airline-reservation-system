namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class BasketSegmentRequest
{
    public Guid OfferId { get; init; }
    public Guid? SessionId { get; init; }
}

public sealed class CreateBasketRequest
{
    public IReadOnlyList<BasketSegmentRequest> Segments { get; init; } = [];
    public string ChannelCode { get; init; } = string.Empty;
    public string? CurrencyCode { get; init; }
    public string? BookingType { get; init; }
    public string? LoyaltyNumber { get; init; }
    public string? CustomerId { get; init; }
    public int PassengerCount { get; init; } = 1;
}
