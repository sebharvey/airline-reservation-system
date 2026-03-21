namespace ReservationSystem.Microservices.Delivery.Application.GetTicket;

/// <summary>
/// Query to retrieve a ticket by its unique identifier.
/// </summary>
public sealed record GetTicketQuery(Guid TicketId);
