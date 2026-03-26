namespace ReservationSystem.Microservices.Identity.Application.VerifyEmailChange;

/// <summary>
/// Command carrying the data needed to confirm an email address change.
/// </summary>
public sealed record VerifyEmailChangeCommand(
    string Token,
    string NewEmail);
