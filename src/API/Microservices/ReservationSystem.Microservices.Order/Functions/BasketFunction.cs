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

    // POST /v1/basket
    [Function("CreateBasket")]
    public async Task<HttpResponseData> CreateBasket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/basket")] HttpRequestData req,
        CancellationToken ct)
    {
        CreateBasketRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateBasketRequest>(
                req.Body, SharedJsonOptions.CamelCase, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateBasket request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ChannelCode))
            return await req.BadRequestAsync("The field 'channelCode' is required.");

        if (request.BookingType == "Reward" &&
            (string.IsNullOrWhiteSpace(request.LoyaltyNumber) || request.TotalPointsAmount is null))
            return await req.BadRequestAsync("'loyaltyNumber' and 'totalPointsAmount' are required for Reward bookings.");

        var command = OrderMapper.ToCommand(request);
        var basket = await _createBasketHandler.HandleAsync(command, ct);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Location", $"/v1/basket/{basket.BasketId}");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(
            OrderMapper.ToCreateResponse(basket), SharedJsonOptions.CamelCase));
        return httpResponse;
    }

    // GET /v1/basket/{basketId}
    [Function("GetBasket")]
    public async Task<HttpResponseData> GetBasket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/basket/{basketId:guid}")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        var basket = await _getBasketHandler.HandleAsync(new GetBasketQuery(basketId), ct);
        if (basket is null)
            return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(OrderMapper.ToResponse(basket));
    }

    // POST /v1/basket/{basketId}/offers
    [Function("AddBasketOffer")]
    public async Task<HttpResponseData> AddOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/basket/{basketId:guid}/offers")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateBasketFlightsCommand(basketId, body);
        try
        {
            var basket = await _updateFlightsHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                basketId = basket.BasketId,
                basketItemId = $"BI-{(basket.TotalFareAmount.HasValue ? Math.Max(1, (int)(basket.TotalFareAmount.Value / 100)) : 1)}",
                totalFareAmount = basket.TotalFareAmount ?? 0m,
                totalAmount = basket.TotalAmount ?? 0m
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PUT /v1/basket/{basketId}/passengers
    [Function("UpdateBasketPassengers")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/passengers")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateBasketPassengersCommand(basketId, body);
        try
        {
            var basket = await _updatePassengersHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new { basketId = basket.BasketId, passengerCount = 0 });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PUT /v1/basket/{basketId}/seats
    [Function("UpdateBasketSeats")]
    public async Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/seats")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateBasketSeatsCommand(basketId, body);
        try
        {
            var basket = await _updateSeatsHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                basketId = basket.BasketId,
                totalSeatAmount = basket.TotalSeatAmount,
                totalAmount = basket.TotalAmount ?? 0m
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PUT /v1/basket/{basketId}/bags
    [Function("UpdateBasketBags")]
    public async Task<HttpResponseData> UpdateBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/bags")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateBasketBagsCommand(basketId, body);
        try
        {
            var basket = await _updateBagsHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                basketId = basket.BasketId,
                totalBagAmount = basket.TotalBagAmount,
                totalAmount = basket.TotalAmount ?? 0m
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PUT /v1/basket/{basketId}/ssrs
    [Function("UpdateBasketSsrs")]
    public async Task<HttpResponseData> UpdateSsrs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/ssrs")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateBasketPassengersCommand(basketId, body);
        try
        {
            var basket = await _updatePassengersHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                basketId = basket.BasketId,
                ssrCount = 0,
                totalAmount = basket.TotalAmount ?? 0m
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/basket/{basketId}/expire
    [Function("ExpireBasket")]
    public async Task<HttpResponseData> ExpireBasket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/basket/{basketId:guid}/expire")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        var expired = await _expireBasketHandler.HandleAsync(new ExpireBasketCommand(basketId), ct);
        return expired
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }
}
