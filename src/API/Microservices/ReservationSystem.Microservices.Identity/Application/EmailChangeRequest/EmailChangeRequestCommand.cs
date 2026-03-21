namespace ReservationSystem.Microservices.Identity.Application.EmailChangeRequest;

/// <summary>
/// Command carrying the data needed to initiate an email change request.
/// </summary>
public sealed record EmailChangeRequestCommand(
    Guid IdentityReference,
    string NewEmail);
