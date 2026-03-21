using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Identity.Application.CreateAccount;
using ReservationSystem.Microservices.Identity.Application.DeleteAccount;
using ReservationSystem.Microservices.Identity.Application.EmailChangeRequest;
using ReservationSystem.Microservices.Identity.Application.VerifyEmail;
using ReservationSystem.Microservices.Identity.Application.VerifyEmailChange;
using ReservationSystem.Microservices.Identity.Models.Mappers;
using ReservationSystem.Microservices.Identity.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Identity.Functions;

/// <summary>
/// HTTP-triggered functions for user account management endpoints.
/// Handles account creation, deletion, email verification, and email change.
/// </summary>
public sealed class AccountFunction
{
    private readonly CreateAccountHandler _createAccountHandler;
    private readonly DeleteAccountHandler _deleteAccountHandler;
    private readonly VerifyEmailHandler _verifyEmailHandler;
    private readonly EmailChangeRequestHandler _emailChangeRequestHandler;
    private readonly VerifyEmailChangeHandler _verifyEmailChangeHandler;
    private readonly ILogger<AccountFunction> _logger;

    public AccountFunction(
        CreateAccountHandler createAccountHandler,
        DeleteAccountHandler deleteAccountHandler,
        VerifyEmailHandler verifyEmailHandler,
        EmailChangeRequestHandler emailChangeRequestHandler,
        VerifyEmailChangeHandler verifyEmailChangeHandler,
        ILogger<AccountFunction> logger)
    {
        _createAccountHandler = createAccountHandler;
        _deleteAccountHandler = deleteAccountHandler;
        _verifyEmailHandler = verifyEmailHandler;
        _emailChangeRequestHandler = emailChangeRequestHandler;
        _verifyEmailChangeHandler = verifyEmailChangeHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts
    // -------------------------------------------------------------------------

    [Function("CreateAccount")]
    public async Task<HttpResponseData> CreateAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateAccountRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateAccountRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateAccount request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = IdentityMapper.ToCommand(request);
        var result = await _createAccountHandler.HandleAsync(command, cancellationToken);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(result, SharedJsonOptions.CamelCase));
        return httpResponse;
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/accounts/{userAccountId:guid}
    // -------------------------------------------------------------------------

    [Function("DeleteAccount")]
    public async Task<HttpResponseData> DeleteAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/accounts/{userAccountId:guid}")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        var command = new DeleteAccountCommand(userAccountId);
        await _deleteAccountHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{userAccountId:guid}/verify-email
    // -------------------------------------------------------------------------

    [Function("VerifyEmail")]
    public async Task<HttpResponseData> VerifyEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{userAccountId:guid}/verify-email")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        // Token expected as a query parameter, parsed manually to avoid System.Web dependency
        var query = req.Url.Query.TrimStart('?');
        var token = query.Split('&')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2 && p[0].Equals("token", StringComparison.OrdinalIgnoreCase))
            .Select(p => Uri.UnescapeDataString(p[1]))
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
            return await req.BadRequestAsync("The 'token' query parameter is required.");

        var command = new VerifyEmailCommand(userAccountId, token);
        await _verifyEmailHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{identityReference:guid}/email/change-request
    // -------------------------------------------------------------------------

    [Function("EmailChangeRequest")]
    public async Task<HttpResponseData> EmailChangeRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{identityReference:guid}/email/change-request")] HttpRequestData req,
        Guid identityReference,
        CancellationToken cancellationToken)
    {
        EmailChangeRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<EmailChangeRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in EmailChangeRequest for identity {IdentityReference}", identityReference);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = new EmailChangeRequestCommand(identityReference, request.NewEmail);
        await _emailChangeRequestHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.Accepted);
    }

    // -------------------------------------------------------------------------
    // POST /v1/email/verify
    // -------------------------------------------------------------------------

    [Function("VerifyEmailChange")]
    public async Task<HttpResponseData> VerifyEmailChange(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/email/verify")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        VerifyEmailChangeRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<VerifyEmailChangeRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in VerifyEmailChange request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = new VerifyEmailChangeCommand(request.VerificationToken);
        await _verifyEmailChangeHandler.HandleAsync(command, cancellationToken);

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
