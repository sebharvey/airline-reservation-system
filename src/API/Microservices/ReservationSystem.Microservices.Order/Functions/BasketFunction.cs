using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Application.CreateBasket;
using ReservationSystem.Microservices.Order.Application.ExpireBasket;
using ReservationSystem.Microservices.Order.Application.GetBasket;
using ReservationSystem.Microservices.Order.Application.UpdateBasketBags;
using ReservationSystem.Microservices.Order.Application.UpdateBasketFlights;
using ReservationSystem.Microservices.Order.Application.UpdateBasketPassengers;
using ReservationSystem.Microservices.Order.Application.UpdateBasketSeats;
using ReservationSystem.Microservices.Order.Models.Mappers;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Functions;

/// <summary>
/// HTTP-triggered functions for Basket operations.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// </summary>
public sealed class BasketFunction
{
    private readonly CreateBasketHandler _createBasketHandler;
    private readonly GetBasketHandler _getBasketHandler;
    private readonly UpdateBasketFlightsHandler _updateFlightsHandler;
    private readonly UpdateBasketSeatsHandler _updateSeatsHandler;
    private readonly UpdateBasketBagsHandler _updateBagsHandler;
    private readonly UpdateBasketPassengersHandler _updatePassengersHandler;
    private readonly ExpireBasketHandler _expireBasketHandler;
    private readonly ILogger<BasketFunction> _logger;

    public BasketFunction(
        CreateBasketHandler createBasketHandler,
        GetBasketHandler getBasketHandler,
        UpdateBasketFlightsHandler updateFlightsHandler,
        UpdateBasketSeatsHandler updateSeatsHandler,
        UpdateBasketBagsHandler updateBagsHandler,
        UpdateBasketPassengersHandler updatePassengersHandler,
        ExpireBasketHandler expireBasketHandler,
        ILogger<BasketFunction> logger)
    {
        _createBasketHandler = createBasketHandler;
        _getBasketHandler = getBasketHandler;
        _updateFlightsHandler = updateFlightsHandler;
        _updateSeatsHandler = updateSeatsHandler;
        _updateBagsHandler = updateBagsHandler;
        _updatePassengersHandler = updatePassengersHandler;
        _expireBasketHandler = expireBasketHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/basket
    // -------------------------------------------------------------------------

    [Function("CreateBasket")]
    public async Task<HttpResponseData> CreateBasket(
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

        if (request is null
            || string.IsNullOrWhiteSpace(request.ChannelCode)
            || string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            return await req.BadRequestAsync("The fields 'channelCode' and 'currencyCode' are required.");
        }

        var command = OrderMapper.ToCommand(request);
        var basket = await _createBasketHandler.HandleAsync(command, cancellationToken);
        var response = OrderMapper.ToCreateResponse(basket);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Location", $"/v1/basket/{basket.BasketId}");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(response, SharedJsonOptions.CamelCase));
        return httpResponse;
    }

    // -------------------------------------------------------------------------
    // GET /v1/basket/{basketId:guid}
    // -------------------------------------------------------------------------

    [Function("GetBasket")]
    public async Task<HttpResponseData> GetBasket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/basket/{basketId:guid}")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        var basket = await _getBasketHandler.HandleAsync(new GetBasketQuery(basketId), cancellationToken);

        if (basket is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId:guid}/flights
    // -------------------------------------------------------------------------

    [Function("UpdateBasketFlights")]
    public async Task<HttpResponseData> UpdateFlights(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/flights")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        UpdateBasketFlightsRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateBasketFlightsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateBasketFlights request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = OrderMapper.ToCommand(basketId, request);
        var basket = await _updateFlightsHandler.HandleAsync(command, cancellationToken);

        if (basket is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId:guid}/seats
    // -------------------------------------------------------------------------

    [Function("UpdateBasketSeats")]
    public async Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/seats")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        UpdateBasketSeatsRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateBasketSeatsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateBasketSeats request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = OrderMapper.ToCommand(basketId, request);
        var basket = await _updateSeatsHandler.HandleAsync(command, cancellationToken);

        if (basket is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId:guid}/bags
    // -------------------------------------------------------------------------

    [Function("UpdateBasketBags")]
    public async Task<HttpResponseData> UpdateBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/bags")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        UpdateBasketBagsRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateBasketBagsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateBasketBags request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = OrderMapper.ToCommand(basketId, request);
        var basket = await _updateBagsHandler.HandleAsync(command, cancellationToken);

        if (basket is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId:guid}/passengers
    // -------------------------------------------------------------------------

    [Function("UpdateBasketPassengers")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/passengers")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        UpdateBasketPassengersRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateBasketPassengersRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateBasketPassengers request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = OrderMapper.ToCommand(basketId, request);
        var basket = await _updatePassengersHandler.HandleAsync(command, cancellationToken);

        if (basket is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/basket/{basketId:guid}/expire
    // -------------------------------------------------------------------------

    [Function("ExpireBasket")]
    public async Task<HttpResponseData> ExpireBasket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/basket/{basketId:guid}/expire")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        var expired = await _expireBasketHandler.HandleAsync(new ExpireBasketCommand(basketId), cancellationToken);

        return expired
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }
}
