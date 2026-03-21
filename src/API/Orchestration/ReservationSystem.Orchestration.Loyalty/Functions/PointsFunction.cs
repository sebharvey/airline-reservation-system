using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.AuthorisePoints;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for loyalty points management.
/// Orchestrates calls to the Customer microservice for points transactions.
/// </summary>
public sealed class PointsFunction
{
    private readonly AuthorisePointsHandler _authorisePointsHandler;
    private readonly ILogger<PointsFunction> _logger;

    public PointsFunction(
        AuthorisePointsHandler authorisePointsHandler,
        ILogger<PointsFunction> logger)
    {
        _authorisePointsHandler = authorisePointsHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/authorise
    // -------------------------------------------------------------------------

    [Function("AuthorisePoints")]
    public async Task<HttpResponseData> Authorise(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/authorise")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var command = new AuthorisePointsCommand(loyaltyNumber);
        var result = await _authorisePointsHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/settle
    // -------------------------------------------------------------------------

    [Function("SettlePoints")]
    public Task<HttpResponseData> Settle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/settle")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/reverse
    // -------------------------------------------------------------------------

    [Function("ReversePoints")]
    public Task<HttpResponseData> Reverse(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/reverse")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/reinstate
    // -------------------------------------------------------------------------

    [Function("ReinstatePoints")]
    public Task<HttpResponseData> Reinstate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/reinstate")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
