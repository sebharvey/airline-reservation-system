using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Loyalty.Application.AuthorisePoints;
using ReservationSystem.Orchestration.Loyalty.Application.TransferPoints;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using ReservationSystem.Orchestration.Loyalty.Validation;
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
    private readonly TransferPointsHandler _transferPointsHandler;
    private readonly ILogger<PointsFunction> _logger;

    public PointsFunction(
        AuthorisePointsHandler authorisePointsHandler,
        TransferPointsHandler transferPointsHandler,
        ILogger<PointsFunction> logger)
    {
        _authorisePointsHandler = authorisePointsHandler;
        _transferPointsHandler = transferPointsHandler;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/customers/{loyaltyNumber}/points/authorise")] HttpRequestData req,
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/customers/{loyaltyNumber}/points/settle")] HttpRequestData req,
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/customers/{loyaltyNumber}/points/reverse")] HttpRequestData req,
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/customers/{loyaltyNumber}/points/reinstate")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/transfer  [Protected]
    // -------------------------------------------------------------------------

    [Function("TransferPoints")]
    [OpenApiOperation(operationId: "TransferPoints", tags: new[] { "Points" }, Summary = "Transfer points to another loyalty account")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The sender's loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(TransferPointsRequest), Required = true, Description = "Recipient loyalty number, recipient email address, and points to transfer")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TransferPointsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request — validation failure, email mismatch, or insufficient points")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — sender or recipient account not found")]
    public async Task<HttpResponseData> Transfer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/v1/customers/{loyaltyNumber}/points/transfer")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<TransferPointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = LoyaltyValidator.ValidateTransferPoints(
            loyaltyNumber, request!.RecipientLoyaltyNumber, request.RecipientEmail, request.Points);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        var command = new TransferPointsCommand(
            SenderLoyaltyNumber: loyaltyNumber,
            RecipientLoyaltyNumber: request.RecipientLoyaltyNumber,
            RecipientEmail: request.RecipientEmail,
            Points: request.Points);

        TransferPointsResponse? result;

        try
        {
            result = await _transferPointsHandler.HandleAsync(command, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Recipient verification failed for transfer from {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insufficient points for transfer from {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync(ex.Message);
        }

        if (result is null)
            return await req.NotFoundAsync("Sender or recipient account not found.");

        return await req.OkJsonAsync(result);
    }
}
