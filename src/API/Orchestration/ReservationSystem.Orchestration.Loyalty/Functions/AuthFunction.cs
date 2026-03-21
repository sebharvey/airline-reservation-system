using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.Login;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for authentication.
/// Orchestrates calls to the Identity microservice.
/// </summary>
public sealed class AuthFunction
{
    private readonly LoginHandler _loginHandler;
    private readonly ILogger<AuthFunction> _logger;

    public AuthFunction(
        LoginHandler loginHandler,
        ILogger<AuthFunction> logger)
    {
        _loginHandler = loginHandler;
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

        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return await req.BadRequestAsync("The fields 'email' and 'password' are required.");
        }

        var command = new LoginCommand(request.Email, request.Password);
        var result = await _loginHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/refresh
    // -------------------------------------------------------------------------

    [Function("RefreshToken")]
    public Task<HttpResponseData> Refresh(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/refresh")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/logout
    // -------------------------------------------------------------------------

    [Function("Logout")]
    public Task<HttpResponseData> Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/logout")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset-request
    // -------------------------------------------------------------------------

    [Function("PasswordResetRequest")]
    public Task<HttpResponseData> PasswordResetRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/reset-request")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/auth/password/reset
    // -------------------------------------------------------------------------

    [Function("PasswordReset")]
    public Task<HttpResponseData> PasswordReset(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password/reset")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
