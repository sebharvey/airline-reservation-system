using ReservationSystem.Microservices.User.Application.AddUser;
using ReservationSystem.Microservices.User.Application.Login;
using ReservationSystem.Microservices.User.Application.UpdateUser;
using ReservationSystem.Microservices.User.Application.SetUserStatus;
using ReservationSystem.Microservices.User.Application.DeleteUser;
using ReservationSystem.Microservices.User.Application.UnlockUser;
using ReservationSystem.Microservices.User.Application.ResetPassword;
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

    public static UpdateUserCommand ToCommand(Guid userId, UpdateUserRequest request) =>
        new(
            UserId: userId,
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email);

    public static SetUserStatusCommand ToCommand(Guid userId, SetUserStatusRequest request) =>
        new(
            UserId: userId,
            IsActive: request.IsActive);

    public static UnlockUserCommand ToUnlockCommand(Guid userId) =>
        new(UserId: userId);

    public static DeleteUserCommand ToDeleteCommand(Guid userId) =>
        new(UserId: userId);

    public static ResetPasswordCommand ToCommand(Guid userId, ResetPasswordRequest request) =>
        new(
            UserId: userId,
            NewPassword: request.NewPassword);
}
