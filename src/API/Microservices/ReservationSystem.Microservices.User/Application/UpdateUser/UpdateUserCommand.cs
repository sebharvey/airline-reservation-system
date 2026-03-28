namespace ReservationSystem.Microservices.User.Application.UpdateUser;

/// <summary>
/// Command to update an employee user account's profile fields.
/// Null fields are left unchanged.
/// </summary>
public sealed record UpdateUserCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email);
