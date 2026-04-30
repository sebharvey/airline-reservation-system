namespace ReservationSystem.Orchestration.Admin.Application.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email);
