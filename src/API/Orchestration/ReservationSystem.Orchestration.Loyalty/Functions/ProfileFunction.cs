using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
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
    public Task<HttpResponseData> GetTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
