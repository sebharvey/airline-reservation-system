using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Application.Login;
using ReservationSystem.Microservices.Identity.Application.Logout;
using ReservationSystem.Microservices.Identity.Application.RefreshToken;
using ReservationSystem.Microservices.Identity.Application.ResetPassword;
using ReservationSystem.Microservices.Identity.Application.ResetPasswordRequest;
using ReservationSystem.Microservices.Identity.Models.Mappers;
using ReservationSystem.Microservices.Identity.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Identity.Functions;

/// <summary>
/// HTTP-triggered functions for authentication endpoints.
/// Handles login, refresh, logout, and password reset flows.
/// </summary>
public sealed class AuthFunction
{
    private readonly LoginHandler _loginHandler;
    private readonly RefreshTokenHandler _refreshTokenHandler;
    private readonly LogoutHandler _logoutHandler;
    private readonly ResetPasswordRequestHandler _resetPasswordRequestHandler;
    private readonly ResetPasswordHandler _resetPasswordHandler;
    private readonly ILogger<AuthFunction> _logger;

    public AuthFunction(
        LoginHandler loginHandler,
        RefreshTokenHandler refreshTokenHandler,
        LogoutHandler logoutHandler,
        ResetPasswordRequestHandler resetPasswordRequestHandler,
        ResetPasswordHandler resetPasswordHandler,
        ILogger<AuthFunction> logger)
    {
        _loginHandler = loginHandler;
        _refreshTokenHandler = refreshTokenHandler;
        _logoutHandler = logoutHandler;
        _resetPasswordRequestHandler = resetPasswordRequestHandler;
        _resetPasswordHandler = resetPasswordHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/login
    // -------------------------------------------------------------------------

    [Function("Login")]
    public async Task<HttpResponseData> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/login")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        LoginRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<LoginRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in Login request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = IdentityMapper.ToCommand(request);
        var result = await _loginHandler.HandleAsync(command, cancellationToken);

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/refresh
    // -------------------------------------------------------------------------

    [Function("RefreshToken")]
    public async Task<HttpResponseData> Refresh(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/refresh")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        RefreshTokenRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<RefreshTokenRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in RefreshToken request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = IdentityMapper.ToCommand(request);
        var result = await _refreshTokenHandler.HandleAsync(command, cancellationToken);

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/logout
    // -------------------------------------------------------------------------

    [Function("Logout")]
    public async Task<HttpResponseData> Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/logout")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        LogoutRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<LogoutRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in Logout request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = new LogoutCommand(request.RefreshToken);
        await _logoutHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset-request
    // -------------------------------------------------------------------------

    [Function("ResetPasswordRequest")]
    public async Task<HttpResponseData> ResetPasswordRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/reset-request")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        ResetPasswordRequestRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<ResetPasswordRequestRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in ResetPasswordRequest request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = new Application.ResetPasswordRequest.ResetPasswordRequestCommand(request.Email);
        await _resetPasswordRequestHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.Accepted);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset
    // -------------------------------------------------------------------------

    [Function("ResetPassword")]
    public async Task<HttpResponseData> ResetPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/reset")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        ResetPasswordRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<ResetPasswordRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in ResetPassword request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = new Application.ResetPassword.ResetPasswordCommand(request.ResetToken, request.NewPassword);
        await _resetPasswordHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
