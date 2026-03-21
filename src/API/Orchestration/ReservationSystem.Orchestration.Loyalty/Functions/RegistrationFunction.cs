using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.Register;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for member registration.
/// Orchestrates calls to the Identity and Customer microservices.
/// </summary>
public sealed class RegistrationFunction
{
    private readonly RegisterHandler _registerHandler;
    private readonly ILogger<RegistrationFunction> _logger;

    public RegistrationFunction(
        RegisterHandler registerHandler,
        ILogger<RegistrationFunction> logger)
    {
        _registerHandler = registerHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/register
    // -------------------------------------------------------------------------

    [Function("RegisterMember")]
    public async Task<HttpResponseData> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/register")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        RegisterRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<RegisterRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in RegisterMember request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.FirstName)
            || string.IsNullOrWhiteSpace(request.LastName))
        {
            return await req.BadRequestAsync("The fields 'email', 'password', 'firstName', and 'lastName' are required.");
        }

        var command = new RegisterCommand(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.PhoneNumber);

        var result = await _registerHandler.HandleAsync(command, cancellationToken);
        return await req.CreatedAsync($"/v1/customers/{result.LoyaltyNumber}/profile", result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{userAccountId}/verify-email
    // -------------------------------------------------------------------------

    [Function("VerifyEmail")]
    public Task<HttpResponseData> VerifyEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{userAccountId:guid}/verify-email")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{identityReference}/email/change-request
    // -------------------------------------------------------------------------

    [Function("EmailChangeRequest")]
    public Task<HttpResponseData> EmailChangeRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{identityReference:guid}/email/change-request")] HttpRequestData req,
        Guid identityReference,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/email/verify
    // -------------------------------------------------------------------------

    [Function("VerifyEmailToken")]
    public Task<HttpResponseData> VerifyEmailToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/email/verify")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
