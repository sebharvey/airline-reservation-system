namespace ReservationSystem.Microservices.Order.Application.RebookOrder;

/// <summary>
/// Command to rebook a passenger onto a new flight under IROPS (Irregular Operations).
/// </summary>
public sealed record RebookOrderCommand(string BookingReference, string RebookData);
