namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class CreateBasketRequest
{
    public Guid? SessionId { get; init; }
    public IReadOnlyList<Guid> OfferIds { get; init; } = [];
    public string ChannelCode { get; init; } = string.Empty;
    public string? CurrencyCode { get; init; }
    public string? BookingType { get; init; }
    public string? LoyaltyNumber { get; init; }
    public string? CustomerId { get; init; }
}
