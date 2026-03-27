using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Loyalty.Application.Login;
using ReservationSystem.Orchestration.Loyalty.Application.Logout;
using ReservationSystem.Orchestration.Loyalty.Application.PasswordReset;
using ReservationSystem.Orchestration.Loyalty.Application.PasswordResetRequest;
using ReservationSystem.Orchestration.Loyalty.Application.RefreshToken;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for authentication.
/// Orchestrates calls to the Identity microservice.
/// Login, refresh, logout, password reset request, and password reset are public routes — no Bearer token required.
/// </summary>
public sealed class AuthFunction
{
    private readonly LoginHandler _loginHandler;
    private readonly RefreshTokenHandler _refreshTokenHandler;
    private readonly LogoutHandler _logoutHandler;
    private readonly PasswordResetRequestHandler _passwordResetRequestHandler;
    private readonly PasswordResetHandler _passwordResetHandler;
    private readonly ILogger<AuthFunction> _logger;

    public AuthFunction(
        LoginHandler loginHandler,
        RefreshTokenHandler refreshTokenHandler,
        LogoutHandler logoutHandler,
        PasswordResetRequestHandler passwordResetRequestHandler,
        PasswordResetHandler passwordResetHandler,
        ILogger<AuthFunction> logger)
    {
        _loginHandler = loginHandler;
        _refreshTokenHandler = refreshTokenHandler;
        _logoutHandler = logoutHandler;
        _passwordResetRequestHandler = passwordResetRequestHandler;
        _passwordResetHandler = passwordResetHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/login
    // -------------------------------------------------------------------------

    [Function("Login")]
    [OpenApiOperation(operationId: "Login", tags: new[] { "Auth" }, Summary = "Login with email and password")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LoginRequest), Required = true, Description = "Request body: email, password")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(LoginResponse), Description = "OK – returns accessToken, refreshToken, expiresAt, tokenType, loyaltyNumber")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized")]
    public async Task<HttpResponseData> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/auth/login")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<LoginRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Email) || string.IsNullOrWhiteSpace(request.Password))
            return await req.BadRequestAsync("The fields 'email' and 'password' are required.");

        try
        {
            var command = new LoginCommand(request.Email, request.Password);
            var result = await _loginHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode is 401)
        {
            return await req.UnauthorizedAsync("Invalid credentials.");
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode is 403)
        {
            return await req.ForbiddenAsync("Account is locked.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/refresh
    // -------------------------------------------------------------------------

    [Function("RefreshToken")]
    [OpenApiOperation(operationId: "RefreshToken", tags: new[] { "Auth" }, Summary = "Refresh an access token using a refresh token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RefreshTokenRequest), Required = true, Description = "Request body: refreshToken")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(RefreshTokenResponse), Description = "OK – returns new accessToken, refreshToken, expiresAt")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized – refresh token invalid or expired")]
    public async Task<HttpResponseData> Refresh(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/auth/refresh")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<RefreshTokenRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.RefreshToken))
            return await req.BadRequestAsync("The field 'refreshToken' is required.");

        try
        {
            var command = new RefreshTokenCommand(request.RefreshToken);
            var result = await _refreshTokenHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode is 401)
        {
            return await req.UnauthorizedAsync("Invalid or expired refresh token.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/logout
    // -------------------------------------------------------------------------

    [Function("Logout")]
    [OpenApiOperation(operationId: "Logout", tags: new[] { "Auth" }, Summary = "Logout and invalidate the refresh token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LogoutRequest), Required = true, Description = "Request body: refreshToken")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/auth/logout")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<LogoutRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.RefreshToken))
            return await req.BadRequestAsync("The field 'refreshToken' is required.");

        await _logoutHandler.HandleAsync(new LogoutCommand(request.RefreshToken), cancellationToken);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset-request
    // -------------------------------------------------------------------------

    [Function("PasswordResetRequest")]
    [OpenApiOperation(operationId: "PasswordResetRequest", tags: new[] { "Auth" }, Summary = "Request a password reset email")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PasswordResetRequestRequest), Required = true, Description = "Request body: email")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Accepted, Description = "Accepted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> PasswordResetRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/auth/password/reset-request")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<PasswordResetRequestRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Email))
            return await req.BadRequestAsync("The field 'email' is required.");

        try
        {
            var command = new PasswordResetRequestCommand(request.Email);
            await _passwordResetRequestHandler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in PasswordResetRequest");
            return await req.InternalServerErrorAsync();
        }

        return req.CreateResponse(HttpStatusCode.Accepted);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset
    // -------------------------------------------------------------------------

    [Function("PasswordReset")]
    [OpenApiOperation(operationId: "PasswordReset", tags: new[] { "Auth" }, Summary = "Reset password with token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(PasswordResetRequest), Required = true, Description = "Request body: token, newPassword")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – invalid or expired token")]
    public async Task<HttpResponseData> PasswordReset(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/auth/password/reset")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<PasswordResetRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            return await req.BadRequestAsync("The fields 'token' and 'newPassword' are required.");

        try
        {
            var command = new PasswordResetCommand(request.Token, request.NewPassword);
            await _passwordResetHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (ArgumentException)
        {
            return await req.BadRequestAsync("Invalid or expired reset token.");
        }
    }
}
