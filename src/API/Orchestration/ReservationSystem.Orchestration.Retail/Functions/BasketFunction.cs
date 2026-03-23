using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Orchestration.Retail.Application.CreateBasket;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for basket management.
/// Orchestrates calls across Order, Offer, Seat, Bag, Payment, and Delivery microservices.
/// </summary>
public sealed class BasketFunction
{
    private readonly CreateBasketHandler _createBasketHandler;
    private readonly ConfirmBasketHandler _confirmBasketHandler;
    private readonly ILogger<BasketFunction> _logger;

    public BasketFunction(
        CreateBasketHandler createBasketHandler,
        ConfirmBasketHandler confirmBasketHandler,
        ILogger<BasketFunction> logger)
    {
        _createBasketHandler = createBasketHandler;
        _confirmBasketHandler = confirmBasketHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/basket
    // -------------------------------------------------------------------------

    [Function("CreateBasket")]
    [OpenApiOperation(operationId: "CreateBasket", tags: new[] { "Basket" }, Summary = "Create a new shopping basket")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBasketRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/basket")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateBasketRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateBasketRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateBasket request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return await req.BadRequestAsync("The field 'customerId' is required.");
        }

        var command = new CreateBasketCommand(request.CustomerId, request.LoyaltyNumber);
        var result = await _createBasketHandler.HandleAsync(command, cancellationToken);
        return await req.CreatedAsync($"/v1/basket/{result.BasketId}", result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/basket/{basketId}
    // -------------------------------------------------------------------------

    [Function("GetBasket")]
    [OpenApiOperation(operationId: "GetBasket", tags: new[] { "Basket" }, Summary = "Get basket by ID")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    public Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/basket/{basketId:guid}")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/flights
    // -------------------------------------------------------------------------

    [Function("UpdateBasketFlights")]
    [OpenApiOperation(operationId: "UpdateBasketFlights", tags: new[] { "Basket" }, Summary = "Update flight selections in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    public Task<HttpResponseData> UpdateFlights(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/flights")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/passengers
    // -------------------------------------------------------------------------

    [Function("UpdateBasketPassengers")]
    [OpenApiOperation(operationId: "UpdateBasketPassengers", tags: new[] { "Basket" }, Summary = "Update passengers in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    public Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/passengers")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/seats
    // -------------------------------------------------------------------------

    [Function("UpdateBasketSeats")]
    [OpenApiOperation(operationId: "UpdateBasketSeats", tags: new[] { "Basket" }, Summary = "Update seat selections in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    public Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/seats")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/bags
    // -------------------------------------------------------------------------

    [Function("UpdateBasketBags")]
    [OpenApiOperation(operationId: "UpdateBasketBags", tags: new[] { "Basket" }, Summary = "Update bag selections in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    public Task<HttpResponseData> UpdateBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/bags")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // POST /v1/basket/{basketId}/confirm
    // -------------------------------------------------------------------------

    [Function("ConfirmBasket")]
    [OpenApiOperation(operationId: "ConfirmBasket", tags: new[] { "Basket" }, Summary = "Confirm basket and process payment")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ConfirmBasketRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Confirm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/basket/{basketId:guid}/confirm")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        ConfirmBasketRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<ConfirmBasketRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in ConfirmBasket request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            return await req.BadRequestAsync("The field 'paymentMethod' is required.");
        }

        var command = new ConfirmBasketCommand(
            basketId,
            request.PaymentMethod,
            request.PaymentToken,
            request.LoyaltyPointsToRedeem);

        var result = await _confirmBasketHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }
}
