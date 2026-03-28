using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Models;
using ReservationSystem.Orchestration.Loyalty.Application.DeleteAccount;
using ReservationSystem.Orchestration.Loyalty.Application.EmailChangeRequest;
using ReservationSystem.Orchestration.Loyalty.Application.GetPreferences;
using ReservationSystem.Orchestration.Loyalty.Application.GetProfile;
using ReservationSystem.Orchestration.Loyalty.Application.GetTransactions;
using ReservationSystem.Orchestration.Loyalty.Application.UpdatePreferences;
using ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;
using ReservationSystem.Orchestration.Loyalty.Application.VerifyEmailChange;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using ReservationSystem.Orchestration.Loyalty.Validation;
using System.Net;

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
    private readonly GetPreferencesHandler _getPreferencesHandler;
    private readonly UpdatePreferencesHandler _updatePreferencesHandler;
    private readonly DeleteAccountHandler _deleteAccountHandler;
    private readonly EmailChangeRequestHandler _emailChangeRequestHandler;
    private readonly VerifyEmailChangeHandler _verifyEmailChangeHandler;
    private readonly ILogger<ProfileFunction> _logger;

    public ProfileFunction(
        GetProfileHandler getProfileHandler,
        UpdateProfileHandler updateProfileHandler,
        GetTransactionsHandler getTransactionsHandler,
        GetPreferencesHandler getPreferencesHandler,
        UpdatePreferencesHandler updatePreferencesHandler,
        DeleteAccountHandler deleteAccountHandler,
        EmailChangeRequestHandler emailChangeRequestHandler,
        VerifyEmailChangeHandler verifyEmailChangeHandler,
        ILogger<ProfileFunction> logger)
    {
        _getProfileHandler = getProfileHandler;
        _updateProfileHandler = updateProfileHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _getPreferencesHandler = getPreferencesHandler;
        _updatePreferencesHandler = updatePreferencesHandler;
        _deleteAccountHandler = deleteAccountHandler;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/profile")] HttpRequestData req,
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/customers/{loyaltyNumber}/profile")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateProfileRequest>(_logger, cancellationToken);
        if (error is not null) return error;

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
            request.Gender,
            request.Nationality,
            request.PhoneNumber,
            request.PreferredLanguage,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrRegion,
            request.PostalCode,
            request.CountryCode);

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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var paged = PagedRequest.From(req);
        var result = await _getTransactionsHandler.HandleAsync(
            new GetTransactionsQuery(loyaltyNumber, paged.Page, paged.PageSize), cancellationToken);

        if (result is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/preferences
    // -------------------------------------------------------------------------

    [Function("GetCustomerPreferences")]
    [OpenApiOperation(operationId: "GetCustomerPreferences", tags: new[] { "Preferences" }, Summary = "Get preference settings")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PreferencesResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetPreferences(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/preferences")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var result = await _getPreferencesHandler.HandleAsync(new GetPreferencesQuery(loyaltyNumber), cancellationToken);

        if (result is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(new PreferencesResponse
        {
            MarketingEnabled = result.MarketingEnabled,
            AnalyticsEnabled = result.AnalyticsEnabled,
            FunctionalEnabled = result.FunctionalEnabled,
            AppNotificationsEnabled = result.AppNotificationsEnabled
        });
    }

    // -------------------------------------------------------------------------
    // PUT /v1/customers/{loyaltyNumber}/preferences
    // -------------------------------------------------------------------------

    [Function("UpdateCustomerPreferences")]
    [OpenApiOperation(operationId: "UpdateCustomerPreferences", tags: new[] { "Preferences" }, Summary = "Replace preference settings")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdatePreferencesRequest), Required = true, Description = "Preference flags")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePreferences(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/customers/{loyaltyNumber}/preferences")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdatePreferencesRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var command = new UpdatePreferencesCommand(
            loyaltyNumber,
            request!.MarketingEnabled,
            request.AnalyticsEnabled,
            request.FunctionalEnabled,
            request.AppNotificationsEnabled);

        var updated = await _updatePreferencesHandler.HandleAsync(command, cancellationToken);

        if (!updated)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/customers/{loyaltyNumber}/account
    // -------------------------------------------------------------------------

    [Function("DeleteLoyaltyAccount")]
    [OpenApiOperation(operationId: "DeleteLoyaltyAccount", tags: new[] { "Profile" }, Summary = "Delete loyalty account")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteAccount(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/customers/{loyaltyNumber}/account")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var deleted = await _deleteAccountHandler.HandleAsync(new DeleteAccountCommand(loyaltyNumber), cancellationToken);

        if (!deleted)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/accounts/{identityReference:guid}/email/change-request")] HttpRequestData req,
        Guid identityReference,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<EmailChangeRequestRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.NewEmail))
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/email/verify")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<VerifyEmailChangeRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Token) || string.IsNullOrWhiteSpace(request.NewEmail))
            return await req.BadRequestAsync("The fields 'token' and 'newEmail' are required.");

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
