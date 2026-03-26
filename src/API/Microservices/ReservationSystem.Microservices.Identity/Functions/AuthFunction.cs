using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Identity.Application.Login;
using ReservationSystem.Microservices.Identity.Application.Logout;
using ReservationSystem.Microservices.Identity.Application.RefreshToken;
using ReservationSystem.Microservices.Identity.Application.ResetPassword;
using ReservationSystem.Microservices.Identity.Application.ResetPasswordRequest;
using ReservationSystem.Microservices.Identity.Application.VerifyToken;
using ReservationSystem.Microservices.Identity.Models.Mappers;
using ReservationSystem.Microservices.Identity.Models.Requests;
using ReservationSystem.Microservices.Identity.Models.Responses;
using ReservationSystem.Microservices.Identity.Validation;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Identity.Functions;

/// <summary>
/// HTTP-triggered functions for authentication endpoints.
/// Handles login, verify, refresh, logout, and password reset flows.
/// </summary>
public sealed class AuthFunction
{
    private readonly LoginHandler _loginHandler;
    private readonly VerifyTokenHandler _verifyTokenHandler;
    private readonly RefreshTokenHandler _refreshTokenHandler;
    private readonly LogoutHandler _logoutHandler;
    private readonly ResetPasswordRequestHandler _resetPasswordRequestHandler;
    private readonly ResetPasswordHandler _resetPasswordHandler;
    private readonly ILogger<AuthFunction> _logger;

    public AuthFunction(
        LoginHandler loginHandler,
        VerifyTokenHandler verifyTokenHandler,
        RefreshTokenHandler refreshTokenHandler,
        LogoutHandler logoutHandler,
        ResetPasswordRequestHandler resetPasswordRequestHandler,
        ResetPasswordHandler resetPasswordHandler,
        ILogger<AuthFunction> logger)
    {
        _loginHandler = loginHandler;
        _verifyTokenHandler = verifyTokenHandler;
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
    [OpenApiOperation(operationId: "Login", tags: new[] { "Auth" }, Summary = "Authenticate a user and return tokens")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LoginRequest), Required = true, Description = "Login credentials: email, password")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(LoginResponse), Description = "OK – returns access token and refresh token")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized – invalid credentials")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Forbidden, Description = "Forbidden – account locked")]
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

        var loginErrors = IdentityValidator.ValidateLogin(request?.Email, request?.Password);

        if (loginErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", loginErrors));

        try
        {
            var command = IdentityMapper.ToCommand(request!);
            var result = await _loginHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (UnauthorizedAccessException)
        {
            var response = req.CreateResponse(HttpStatusCode.Unauthorized);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(
                new { error = "Invalid credentials." }, SharedJsonOptions.CamelCase));
            return response;
        }
        catch (InvalidOperationException)
        {
            var response = req.CreateResponse(HttpStatusCode.Forbidden);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(
                new { error = "Account is locked." }, SharedJsonOptions.CamelCase));
            return response;
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/verify
    // -------------------------------------------------------------------------

    [Function("VerifyToken")]
    [OpenApiOperation(operationId: "VerifyToken", tags: new[] { "Auth" }, Summary = "Verify an access token and return its claims")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(VerifyTokenRequest), Required = true, Description = "Verify request: accessToken")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(VerifyTokenResponse), Description = "OK – token is valid")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized – token invalid or expired")]
    public async Task<HttpResponseData> Verify(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/verify")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        VerifyTokenRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<VerifyTokenRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in VerifyToken request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        var accessTokenErrors = IdentityValidator.ValidateRequiredToken(request?.AccessToken, "accessToken");

        if (accessTokenErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", accessTokenErrors));

        try
        {
            var command = new VerifyTokenCommand(request.AccessToken);
            var result = await _verifyTokenHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (UnauthorizedAccessException)
        {
            var response = req.CreateResponse(HttpStatusCode.Unauthorized);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(
                new { error = "Invalid or expired access token." }, SharedJsonOptions.CamelCase));
            return response;
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/refresh
    // -------------------------------------------------------------------------

    [Function("RefreshToken")]
    [OpenApiOperation(operationId: "RefreshToken", tags: new[] { "Auth" }, Summary = "Refresh an access token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RefreshTokenRequest), Required = true, Description = "Refresh token request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(RefreshTokenResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized")]
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

        var refreshErrors = IdentityValidator.ValidateRequiredToken(request?.RefreshToken, "refreshToken");

        if (refreshErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", refreshErrors));

        try
        {
            var command = IdentityMapper.ToCommand(request!);
            var result = await _refreshTokenHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (UnauthorizedAccessException)
        {
            var response = req.CreateResponse(HttpStatusCode.Unauthorized);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(
                new { error = "Invalid or expired refresh token." }, SharedJsonOptions.CamelCase));
            return response;
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/logout
    // -------------------------------------------------------------------------

    [Function("Logout")]
    [OpenApiOperation(operationId: "Logout", tags: new[] { "Auth" }, Summary = "Logout and invalidate refresh token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LogoutRequest), Required = true, Description = "Logout request: refreshToken")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
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

        var logoutErrors = IdentityValidator.ValidateRequiredToken(request?.RefreshToken, "refreshToken");

        if (logoutErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", logoutErrors));

        var command = new LogoutCommand(request!.RefreshToken);
        await _logoutHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.OK);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset-request
    // -------------------------------------------------------------------------

    [Function("ResetPasswordRequest")]
    [OpenApiOperation(operationId: "ResetPasswordRequest", tags: new[] { "Auth" }, Summary = "Request a password reset email")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ResetPasswordRequestRequest), Required = true, Description = "Reset request: email")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Accepted, Description = "Accepted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
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

        var emailErrors = IdentityValidator.ValidateEmailField(request?.Email);

        if (emailErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", emailErrors));

        try
        {
            var command = new Application.ResetPasswordRequest.ResetPasswordRequestCommand(request!.Email);
            await _resetPasswordRequestHandler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ResetPasswordRequest");
            return await req.InternalServerErrorAsync();
        }

        return req.CreateResponse(HttpStatusCode.Accepted);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset
    // -------------------------------------------------------------------------

    [Function("ResetPassword")]
    [OpenApiOperation(operationId: "ResetPassword", tags: new[] { "Auth" }, Summary = "Reset password with token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ResetPasswordRequest), Required = true, Description = "Reset request: token, newPassword")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – invalid or expired token")]
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

        var resetErrors = IdentityValidator.ValidateResetPassword(request?.Token, request?.NewPassword);

        if (resetErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", resetErrors));

        try
        {
            var command = new Application.ResetPassword.ResetPasswordCommand(request!.Token, request.NewPassword);
            await _resetPasswordHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (ArgumentException)
        {
            return await req.BadRequestAsync("Invalid or expired reset token.");
        }
    }
}
