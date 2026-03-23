using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.GetProfile;
using ReservationSystem.Orchestration.Loyalty.Application.GetTransactions;
using ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for customer profile and transaction management.
/// Orchestrates calls to the Customer microservice.
/// All routes require a valid Bearer token (enforced by TokenVerificationMiddleware).
/// </summary>
public sealed class ProfileFunction
{
    private readonly GetProfileHandler _getProfileHandler;
    private readonly UpdateProfileHandler _updateProfileHandler;
    private readonly GetTransactionsHandler _getTransactionsHandler;
    private readonly ILogger<ProfileFunction> _logger;

    public ProfileFunction(
        GetProfileHandler getProfileHandler,
        UpdateProfileHandler updateProfileHandler,
        GetTransactionsHandler getTransactionsHandler,
        ILogger<ProfileFunction> logger)
    {
        _getProfileHandler = getProfileHandler;
        _updateProfileHandler = updateProfileHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/profile
    // -------------------------------------------------------------------------

    [Function("GetCustomerProfile")]
    [OpenApiOperation(operationId: "GetCustomerProfile", tags: new[] { "Profile" }, Summary = "Get customer profile")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
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
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Updatable fields: givenName, surname, dateOfBirth, nationality, phoneNumber, preferredLanguage")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/customers/{loyaltyNumber}/profile")] HttpRequestData req,
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

        var command = new UpdateProfileCommand(
            loyaltyNumber,
            request.GivenName,
            request.Surname,
            request.DateOfBirth,
            request.Nationality,
            request.PhoneNumber,
            request.PreferredLanguage);

        var found = await _updateProfileHandler.HandleAsync(command, cancellationToken);

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
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
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
}
