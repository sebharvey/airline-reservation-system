namespace ReservationSystem.Microservices.Order.Application.UpdateBasketSeats;

/// <summary>
/// Command to add or replace seat selections in a basket.
/// </summary>
public sealed record UpdateBasketSeatsCommand(Guid BasketId, string SeatsData);
