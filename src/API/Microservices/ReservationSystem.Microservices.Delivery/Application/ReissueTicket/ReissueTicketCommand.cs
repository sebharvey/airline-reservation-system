namespace ReservationSystem.Microservices.Delivery.Application.ReissueTicket;

/// <summary>
/// Command to reissue an existing ticket with a new e-ticket number.
/// </summary>
public sealed record ReissueTicketCommand(
    Guid ManifestId,
    Guid TicketId,
    string NewETicketNumber);
