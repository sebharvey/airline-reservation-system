namespace ReservationSystem.Microservices.Order.Application.UpdateBasketPassengers;

/// <summary>
/// Command to update passenger details within a basket.
/// </summary>
public sealed record UpdateBasketPassengersCommand(Guid BasketId, string PassengersData);
