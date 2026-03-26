using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.EmailChangeRequest;
using ReservationSystem.Orchestration.Loyalty.Application.GetProfile;
using ReservationSystem.Orchestration.Loyalty.Application.GetTransactions;
using ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;
using ReservationSystem.Orchestration.Loyalty.Application.VerifyEmailChange;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using ReservationSystem.Orchestration.Loyalty.Validation;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for customer profile and transaction management.
/// Orchestrates calls to the Customer microservice.
/// All routes require a valid Bearer token (enforced by TokenVerificationMiddleware),
/// except VerifyEmailChange which is a public link-click endpoint.
/// </summary>
public sealed class ProfileFunction
{
    private readonly GetProfileHandler _getProfileHandler;
    private readonly UpdateProfileHandler _updateProfileHandler;
    private readonly GetTransactionsHandler _getTransactionsHandler;
    private readonly EmailChangeRequestHandler _emailChangeRequestHandler;
    private readonly VerifyEmailChangeHandler _verifyEmailChangeHandler;
    private readonly ILogger<ProfileFunction> _logger;

    public ProfileFunction(
        GetProfileHandler getProfileHandler,
        UpdateProfileHandler updateProfileHandler,
        GetTransactionsHandler getTransactionsHandler,
        EmailChangeRequestHandler emailChangeRequestHandler,
        VerifyEmailChangeHandler verifyEmailChangeHandler,
        ILogger<ProfileFunction> logger)
    {
        _getProfileHandler = getProfileHandler;
        _updateProfileHandler = updateProfileHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _emailChangeRequestHandler = emailChangeRequestHandler;
        _verifyEmailChangeHandler = verifyEmailChangeHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/profile
    // -------------------------------------------------------------------------

    [Function("GetCustomerProfile")]
    [OpenApiOperation(operationId: "GetCustomerProfile", tags: new[] { "Profile" }, Summary = "Get customer profile")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProfileResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/customers/{loyaltyNumber}/profile")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var userEmail = req.FunctionContext.Items.TryGetValue("UserEmail", out var emailObj) && emailObj is string email
            ? email
            : string.Empty;

        var profile = await _getProfileHandler.HandleAsync(
            new GetProfileQuery(loyaltyNumber, userEmail), cancellationToken);

        if (profile is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(profile);
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/customers/{loyaltyNumber}/profile
    // -------------------------------------------------------------------------

    [Function("UpdateCustomerProfile")]
    [OpenApiOperation(operationId: "UpdateCustomerProfile", tags: new[] { "Profile" }, Summary = "Update customer profile")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateProfileRequest), Required = true, Description = "Updatable fields: givenName, surname, dateOfBirth, nationality, phoneNumber, preferredLanguage")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "api/v1/customers/{loyaltyNumber}/profile")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        UpdateProfileRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateProfileRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateProfile request for {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var validationErrors = LoyaltyValidator.ValidateUpdateProfile(
            request.GivenName, request.Surname, request.DateOfBirth,
            request.Nationality, request.PhoneNumber, request.PreferredLanguage);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        var command = new UpdateProfileCommand(
            loyaltyNumber,
            request.GivenName,
            request.Surname,
            request.DateOfBirth,
            request.Nationality,
            request.PhoneNumber,
            request.PreferredLanguage);

        bool found;

        try
        {
            found = await _updateProfileHandler.HandleAsync(command, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        if (!found)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/transactions
    // -------------------------------------------------------------------------

    [Function("GetCustomerTransactions")]
    [OpenApiOperation(operationId: "GetCustomerTransactions", tags: new[] { "Profile" }, Summary = "Get loyalty transactions")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiParameter(name: "page", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page number (default 1)")]
    [OpenApiParameter(name: "pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page size (default 20)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TransactionsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        int page = 1;
        int pageSize = 20;

        var pageParam = req.Query["page"];
        if (!string.IsNullOrEmpty(pageParam) && int.TryParse(pageParam, out var parsedPage))
            page = parsedPage;

        var pageSizeParam = req.Query["pageSize"];
        if (!string.IsNullOrEmpty(pageSizeParam) && int.TryParse(pageSizeParam, out var parsedPageSize))
            pageSize = parsedPageSize;

        var result = await _getTransactionsHandler.HandleAsync(
            new GetTransactionsQuery(loyaltyNumber, page, pageSize), cancellationToken);

        if (result is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/accounts/{identityReference}/email/change-request  [Protected]
    // -------------------------------------------------------------------------

    [Function("EmailChangeRequest")]
    [OpenApiOperation(operationId: "EmailChangeRequest", tags: new[] { "Profile" }, Summary = "Request an email address change")]
    [OpenApiParameter(name: "identityReference", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The user account identity reference (GUID)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(EmailChangeRequestRequest), Required = true, Description = "Request body: newEmail")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Accepted, Description = "Accepted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – email already registered")]
    public async Task<HttpResponseData> EmailChangeRequest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/accounts/{identityReference:guid}/email/change-request")] HttpRequestData req,
        Guid identityReference,
        CancellationToken cancellationToken)
    {
        EmailChangeRequestRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<EmailChangeRequestRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in EmailChangeRequest for {IdentityReference}", identityReference);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.NewEmail))
            return await req.BadRequestAsync("The field 'newEmail' is required.");

        try
        {
            var command = new EmailChangeRequestCommand(identityReference, request.NewEmail);
            await _emailChangeRequestHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"No account found for identity reference '{identityReference}'.");
        }
        catch (InvalidOperationException)
        {
            return await req.ConflictAsync("The new email address is already registered to another account.");
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/email/verify  [Public – link-click from email]
    // -------------------------------------------------------------------------

    [Function("VerifyEmailChange")]
    [OpenApiOperation(operationId: "VerifyEmailChange", tags: new[] { "Profile" }, Summary = "Verify email change with token")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(VerifyEmailChangeRequest), Required = true, Description = "Request body: token, newEmail")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request – invalid or expired token")]
    public async Task<HttpResponseData> VerifyEmailChange(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/email/verify")] HttpRequestData req,
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
            _logger.LogWarning(ex, "Invalid JSON in VerifyEmailChange");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.NewEmail))
        {
            return await req.BadRequestAsync("The fields 'token' and 'newEmail' are required.");
        }

        try
        {
            var command = new VerifyEmailChangeCommand(request.Token, request.NewEmail);
            await _verifyEmailChangeHandler.HandleAsync(command, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (ArgumentException)
        {
            return await req.BadRequestAsync("Invalid or expired verification token.");
        }
    }
}
