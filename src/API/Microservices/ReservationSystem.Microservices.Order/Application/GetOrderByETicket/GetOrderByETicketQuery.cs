namespace ReservationSystem.Microservices.Order.Application.GetOrderByETicket;

/// <summary>
/// Query to retrieve an order by e-ticket number.
/// </summary>
public sealed record GetOrderByETicketQuery(string ETicketNumber);
