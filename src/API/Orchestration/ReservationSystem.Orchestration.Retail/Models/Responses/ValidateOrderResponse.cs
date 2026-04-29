namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class ValidateOrderResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
