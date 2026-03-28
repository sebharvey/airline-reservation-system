using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Loyalty.Application.Register;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using ReservationSystem.Orchestration.Loyalty.Validation;
using System.Net;

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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/register")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<RegisterRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = LoyaltyValidator.ValidateRegister(
            request!.Email, request.Password, request.GivenName, request.Surname,
            request.DateOfBirth, request.PhoneNumber, request.PreferredLanguage);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        var command = new RegisterCommand(
            request!.Email,
            request.Password,
            request.GivenName,
            request.Surname,
            request.DateOfBirth,
            request.PhoneNumber,
            request.Nationality,
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/accounts/{userAccountId:guid}/verify-email")] HttpRequestData req,
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{userAccountId:guid}/verify-email")] HttpRequestData req,
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

}
