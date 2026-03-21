namespace ReservationSystem.Microservices.Order.Application.UpdateBasketSsrs;

/// <summary>
/// Command to add or replace SSR selections in a basket.
/// </summary>
public sealed record UpdateBasketSsrsCommand(Guid BasketId, string SsrsData);
