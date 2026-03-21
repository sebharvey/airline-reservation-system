namespace ReservationSystem.Microservices.Order.Application.UpdateBasketBags;

/// <summary>
/// Command to add or replace bag ancillary selections in a basket.
/// </summary>
public sealed record UpdateBasketBagsCommand(Guid BasketId, string BagsData);
