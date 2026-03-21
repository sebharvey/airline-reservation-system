namespace ReservationSystem.Microservices.Identity.Application.VerifyEmail;

/// <summary>
/// Command carrying the data needed to verify a user account's email address.
/// </summary>
public sealed record VerifyEmailCommand(
    Guid UserAccountId);
