using ReservationSystem.Microservices.Identity.Application.CreateAccount;
using ReservationSystem.Microservices.Identity.Application.Login;
using ReservationSystem.Microservices.Identity.Application.RefreshToken;
using ReservationSystem.Microservices.Identity.Domain.Entities;
using ReservationSystem.Microservices.Identity.Models.Requests;

namespace ReservationSystem.Microservices.Identity.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of Identity resources.
///
/// Mapping directions:
///
///   HTTP request  →  Application command
///   Domain entity →  HTTP response
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class IdentityMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static LoginCommand ToCommand(LoginRequest request) =>
        new(
            Email: request.Email,
            Password: request.Password,
            DeviceHint: request.DeviceHint);

    public static RefreshTokenCommand ToCommand(RefreshTokenRequest request) =>
        new(
            Token: request.Token,
            DeviceHint: request.DeviceHint);

    public static CreateAccountCommand ToCommand(CreateAccountRequest request) =>
        new(
            Email: request.Email,
            Password: request.Password);
}
