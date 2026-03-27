using ReservationSystem.Microservices.User.Application.AddUser;
using ReservationSystem.Microservices.User.Application.Login;
using ReservationSystem.Microservices.User.Models.Requests;

namespace ReservationSystem.Microservices.User.Models.Mappers;

/// <summary>
/// Static mapping methods between HTTP request models and application commands.
///
/// Mapping directions:
///   HTTP request  →  Application command
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class UserMapper
{
    public static AddUserCommand ToCommand(AddUserRequest request) =>
        new(
            Username: request.Username,
            Email: request.Email,
            Password: request.Password,
            FirstName: request.FirstName,
            LastName: request.LastName);

    public static LoginCommand ToCommand(LoginRequest request) =>
        new(
            Username: request.Username,
            Password: request.Password);
}
