using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.Register;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using ReservationSystem.Orchestration.Loyalty.Validation;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for member registration.
/// Orchestrates calls to the Identity and Customer microservices.
/// </summary>
public sealed class RegistrationFunction
{
    private readonly RegisterHandler _registerHandler;
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly ILogger<RegistrationFunction> _logger;

    public RegistrationFunction(
        RegisterHandler registerHandler,
        IdentityServiceClient identityServiceClient,
        ILogger<RegistrationFunction> logger)
    {
        _registerHandler = registerHandler;
        _identityServiceClient = identityServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/register
    // -------------------------------------------------------------------------

    [Function("RegisterMember")]
    [OpenApiOperation(operationId: "RegisterMember", tags: new[] { "Registration" }, Summary = "Register a new loyalty member")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RegisterRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(RegisterMemberResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/register")] HttpRequestData req,
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

        var validationErrors = LoyaltyValidator.ValidateRegister(
            request?.Email, request?.Password, request?.GivenName, request?.Surname,
            request?.DateOfBirth, request?.PhoneNumber, request?.PreferredLanguage);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        var command = new RegisterCommand(
            request!.Email,
            request.Password,
            request.GivenName,
            request.Surname,
            request.DateOfBirth,
            request.PhoneNumber,
            request.PreferredLanguage);

        try
        {
            var result = await _registerHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync(
                $"/v1/customers/{result.LoyaltyNumber}/profile",
                new { loyaltyNumber = result.LoyaltyNumber });
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException)
        {
            return await req.ConflictAsync("An account with this email address is already registered.");
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/accounts/{userAccountId}/verify-email
    // -------------------------------------------------------------------------

    [Function("GetVerifyEmail")]
    [OpenApiOperation(operationId: "GetVerifyEmail", tags: new[] { "Registration" }, Summary = "Verify email address")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user account identifier")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetVerifyEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/accounts/{userAccountId:guid}/verify-email")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _identityServiceClient.GetVerifyEmailAsync(userAccountId, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{userAccountId}/verify-email
    // -------------------------------------------------------------------------

    [Function("VerifyEmail")]
    [OpenApiOperation(operationId: "VerifyEmail", tags: new[] { "Registration" }, Summary = "Verify email address")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user account identifier")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> VerifyEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/accounts/{userAccountId:guid}/verify-email")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _identityServiceClient.VerifyEmailAsync(userAccountId, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No user account found for ID '{userAccountId}'.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{userAccountId}/email/change-request
    // -------------------------------------------------------------------------

    [Function("EmailChangeRequest")]
    [OpenApiOperation(operationId: "EmailChangeRequest", tags: new[] { "Registration" }, Summary = "Request email change")]
    [OpenApiParameter(name: "userAccountId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user account ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(EmailChangeResponse), Description = "OK")]
    public Task<HttpResponseData> EmailChangeRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/accounts/{userAccountId:guid}/email/change-request")] HttpRequestData req,
        Guid userAccountId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/email/verify
    // -------------------------------------------------------------------------

    [Function("VerifyEmailToken")]
    [OpenApiOperation(operationId: "VerifyEmailToken", tags: new[] { "Registration" }, Summary = "Verify email change token")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(EmailChangeResponse), Description = "OK")]
    public Task<HttpResponseData> VerifyEmailToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/email/verify")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
