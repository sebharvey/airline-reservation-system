namespace ReservationSystem.Orchestration.Retail.Models.Requests;

public sealed class CreateBasketRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string? LoyaltyNumber { get; init; }
}
