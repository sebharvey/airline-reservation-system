namespace ReservationSystem.Microservices.Order.Application.GetRecentOrders;

/// <summary>
/// Query to retrieve the most recently created orders.
/// </summary>
public sealed record GetRecentOrdersQuery(int Limit);
