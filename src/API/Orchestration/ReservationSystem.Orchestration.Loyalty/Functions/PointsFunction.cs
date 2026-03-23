using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.AuthorisePoints;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using System.Net;
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
    [OpenApiOperation(operationId: "AuthorisePoints", tags: new[] { "Points" }, Summary = "Authorise a points transaction")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PointsOperationResponse), Description = "OK")]
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
    [OpenApiOperation(operationId: "SettlePoints", tags: new[] { "Points" }, Summary = "Settle a points transaction")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PointsOperationResponse), Description = "OK")]
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
    [OpenApiOperation(operationId: "ReversePoints", tags: new[] { "Points" }, Summary = "Reverse a points transaction")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PointsOperationResponse), Description = "OK")]
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
    [OpenApiOperation(operationId: "ReinstatePoints", tags: new[] { "Points" }, Summary = "Reinstate reversed points")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PointsOperationResponse), Description = "OK")]
    public Task<HttpResponseData> Reinstate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/reinstate")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
