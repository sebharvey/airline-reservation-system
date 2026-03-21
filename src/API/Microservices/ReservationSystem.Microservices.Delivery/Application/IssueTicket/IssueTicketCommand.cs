namespace ReservationSystem.Microservices.Delivery.Application.IssueTicket;

/// <summary>
/// Command to issue a new electronic ticket on a manifest.
/// </summary>
public sealed record IssueTicketCommand(
    Guid ManifestId,
    Guid PassengerId,
    Guid SegmentId,
    string ETicketNumber);
