using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Order.Application.CreateBasket;
using ReservationSystem.Microservices.Order.Application.ExpireBasket;
using ReservationSystem.Microservices.Order.Application.GetBasket;
using ReservationSystem.Microservices.Order.Application.UpdateBasketBags;
using ReservationSystem.Microservices.Order.Application.UpdateBasketFlights;
using ReservationSystem.Microservices.Order.Application.UpdateBasketPassengers;
using ReservationSystem.Microservices.Order.Application.UpdateBasketSeats;
using ReservationSystem.Microservices.Order.Application.UpdateBasketSsrs;
using ReservationSystem.Microservices.Order.Models.Mappers;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Microservices.Order.Models.Responses;
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
    private readonly UpdateBasketSsrsHandler _updateSsrsHandler;
    private readonly ExpireBasketHandler _expireBasketHandler;
    private readonly ILogger<BasketFunction> _logger;

    public BasketFunction(
        CreateBasketHandler createBasketHandler,
        GetBasketHandler getBasketHandler,
        UpdateBasketFlightsHandler updateFlightsHandler,
        UpdateBasketSeatsHandler updateSeatsHandler,
        UpdateBasketBagsHandler updateBagsHandler,
        UpdateBasketPassengersHandler updatePassengersHandler,
        UpdateBasketSsrsHandler updateSsrsHandler,
        ExpireBasketHandler expireBasketHandler,
        ILogger<BasketFunction> logger)
    {
        _createBasketHandler = createBasketHandler;
        _getBasketHandler = getBasketHandler;
        _updateFlightsHandler = updateFlightsHandler;
        _updateSeatsHandler = updateSeatsHandler;
        _updateBagsHandler = updateBagsHandler;
        _updatePassengersHandler = updatePassengersHandler;
        _updateSsrsHandler = updateSsrsHandler;
        _expireBasketHandler = expireBasketHandler;
        _logger = logger;
    }

    // POST /v1/basket
    [Function("CreateBasket")]
    [OpenApiOperation(operationId: "CreateBasket", tags: new[] { "Basket" }, Summary = "Create a new basket")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBasketRequest), Required = true, Description = "The basket to create")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateBasketResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
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
    [OpenApiOperation(operationId: "GetBasket", tags: new[] { "Basket" }, Summary = "Get a basket by ID")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
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
    [OpenApiOperation(operationId: "AddBasketOffer", tags: new[] { "Basket" }, Summary = "Add a flight offer to a basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The flight offer to add")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AddBasketOfferResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> AddOffer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/basket/{basketId:guid}/offers")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        // Validate offer expiry before processing
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("offerExpiresAt", out var expiresAtProp))
            {
                var offerExpiresAt = expiresAtProp.GetDateTime();
                if (offerExpiresAt <= DateTime.UtcNow)
                    return await req.GoneAsync("Offer has expired and is no longer available.");
            }
        }
        catch (JsonException) { /* Let the handler deal with malformed JSON */ }

        // Determine next basket item ID by counting existing flight offers
        var existingBasket = await _getBasketHandler.HandleAsync(new GetBasketQuery(basketId), ct);
        var nextItemNumber = 1;
        if (existingBasket is not null)
        {
            try
            {
                using var basketDoc = JsonDocument.Parse(existingBasket.BasketData);
                if (basketDoc.RootElement.TryGetProperty("flightOffers", out var offers) &&
                    offers.ValueKind == JsonValueKind.Array)
                {
                    nextItemNumber = offers.GetArrayLength() + 1;
                }
            }
            catch { }
        }

        var command = new UpdateBasketFlightsCommand(basketId, body);
        try
        {
            var basket = await _updateFlightsHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                basketId = basket.BasketId,
                basketItemId = $"BI-{nextItemNumber}",
                totalFareAmount = basket.TotalFareAmount ?? 0m,
                totalAmount = basket.TotalAmount ?? 0m
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PUT /v1/basket/{basketId}/passengers
    [Function("UpdateBasketPassengers")]
    [OpenApiOperation(operationId: "UpdateBasketPassengers", tags: new[] { "Basket" }, Summary = "Update passenger details in a basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The passenger update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateBasketPassengersResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/passengers")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var passengerCount = 0;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("passengers", out var passengers) &&
                passengers.ValueKind == JsonValueKind.Array)
            {
                passengerCount = passengers.GetArrayLength();
            }
        }
        catch { }

        var command = new UpdateBasketPassengersCommand(basketId, body);
        try
        {
            var basket = await _updatePassengersHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new { basketId = basket.BasketId, passengerCount });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PUT /v1/basket/{basketId}/seats
    [Function("UpdateBasketSeats")]
    [OpenApiOperation(operationId: "UpdateBasketSeats", tags: new[] { "Basket" }, Summary = "Update seat selections in a basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The seat update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateBasketAmountResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
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
    [OpenApiOperation(operationId: "UpdateBasketBags", tags: new[] { "Basket" }, Summary = "Update bag selections in a basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The bag update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateBasketAmountResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
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
    [OpenApiOperation(operationId: "UpdateBasketSsrs", tags: new[] { "Basket" }, Summary = "Update SSR selections in a basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket ID")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The SSR update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateBasketSsrsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSsrs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/ssrs")] HttpRequestData req,
        Guid basketId, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var ssrCount = 0;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("ssrSelections", out var ssrSelections) &&
                ssrSelections.ValueKind == JsonValueKind.Array)
            {
                ssrCount = ssrSelections.GetArrayLength();
            }
        }
        catch { }

        var command = new UpdateBasketSsrsCommand(basketId, body);
        try
        {
            var basket = await _updateSsrsHandler.HandleAsync(command, ct);
            if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                basketId = basket.BasketId,
                ssrCount,
                totalAmount = basket.TotalAmount ?? 0m
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/basket/{basketId}/expire
    [Function("ExpireBasket")]
    [OpenApiOperation(operationId: "ExpireBasket", tags: new[] { "Basket" }, Summary = "Expire a basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Expired")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
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
