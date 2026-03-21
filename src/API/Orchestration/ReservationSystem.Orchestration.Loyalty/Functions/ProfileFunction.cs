using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Loyalty.Application.GetProfile;
using ReservationSystem.Orchestration.Loyalty.Application.UpdateProfile;
using System.Net;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for customer profile management.
/// Orchestrates calls to the Customer microservice.
/// </summary>
public sealed class ProfileFunction
{
    private readonly GetProfileHandler _getProfileHandler;
    private readonly UpdateProfileHandler _updateProfileHandler;
    private readonly ILogger<ProfileFunction> _logger;

    public ProfileFunction(
        GetProfileHandler getProfileHandler,
        UpdateProfileHandler updateProfileHandler,
        ILogger<ProfileFunction> logger)
    {
        _getProfileHandler = getProfileHandler;
        _updateProfileHandler = updateProfileHandler;
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
        var profile = await _getProfileHandler.HandleAsync(new GetProfileQuery(loyaltyNumber), cancellationToken);

        if (profile is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(profile);
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/customers/{loyaltyNumber}/profile
    // -------------------------------------------------------------------------

    [Function("UpdateCustomerProfile")]
    [OpenApiOperation(operationId: "UpdateCustomerProfile", tags: new[] { "Profile" }, Summary = "Update customer profile")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content")]
    public async Task<HttpResponseData> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/customers/{loyaltyNumber}/profile")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        await _updateProfileHandler.HandleAsync(new UpdateProfileCommand(loyaltyNumber), cancellationToken);
        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/transactions
    // -------------------------------------------------------------------------

    [Function("GetCustomerTransactions")]
    [OpenApiOperation(operationId: "GetCustomerTransactions", tags: new[] { "Profile" }, Summary = "Get loyalty transactions")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    public Task<HttpResponseData> GetTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
