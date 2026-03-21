using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Disruption.Application.HandleDelay;
using ReservationSystem.Orchestration.Disruption.Application.HandleCancellation;
using ReservationSystem.Orchestration.Disruption.Models.Requests;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Disruption.Functions;

/// <summary>
/// HTTP-triggered functions for operational disruption handling.
/// Orchestrates calls across Offer, Order, Delivery, Customer, and Payment microservices
/// to manage flight delays and cancellations including IROPS rebooking.
/// </summary>
public sealed class DisruptionFunction
{
    private readonly HandleDelayHandler _handleDelayHandler;
    private readonly HandleCancellationHandler _handleCancellationHandler;
    private readonly ILogger<DisruptionFunction> _logger;

    public DisruptionFunction(
        HandleDelayHandler handleDelayHandler,
        HandleCancellationHandler handleCancellationHandler,
        ILogger<DisruptionFunction> logger)
    {
        _handleDelayHandler = handleDelayHandler;
        _handleCancellationHandler = handleCancellationHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/disruptions/delay
    // -------------------------------------------------------------------------

    [Function("HandleDelay")]
    [OpenApiOperation(operationId: "HandleDelay", tags: new[] { "Disruptions" }, Summary = "Handle a flight delay")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> HandleDelay(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/disruptions/delay")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        HandleDelayRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<HandleDelayRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in HandleDelay request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.FlightNumber)
            || request.DelayMinutes <= 0)
        {
            return await req.BadRequestAsync("The fields 'flightNumber' and 'delayMinutes' (greater than 0) are required.");
        }

        var command = new HandleDelayCommand(
            request.FlightNumber,
            request.ScheduledDeparture,
            request.DelayMinutes,
            request.Reason);

        var result = await _handleDelayHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/disruptions/cancellation
    // -------------------------------------------------------------------------

    [Function("HandleCancellation")]
    [OpenApiOperation(operationId: "HandleCancellation", tags: new[] { "Disruptions" }, Summary = "Handle a flight cancellation with optional IROPS rebooking")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> HandleCancellation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/disruptions/cancellation")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        HandleCancellationRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<HandleCancellationRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in HandleCancellation request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null
            || string.IsNullOrWhiteSpace(request.FlightNumber))
        {
            return await req.BadRequestAsync("The field 'flightNumber' is required.");
        }

        var command = new HandleCancellationCommand(
            request.FlightNumber,
            request.ScheduledDeparture,
            request.Reason,
            request.EnableIropsRebooking);

        var result = await _handleCancellationHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }
}
