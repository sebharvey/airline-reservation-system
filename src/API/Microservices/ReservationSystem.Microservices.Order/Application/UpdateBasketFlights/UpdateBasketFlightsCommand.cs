namespace ReservationSystem.Microservices.Order.Application.UpdateBasketFlights;

/// <summary>
/// Command to add or replace flight selections in a basket.
/// </summary>
public sealed record UpdateBasketFlightsCommand(Guid BasketId, string FlightsData);
