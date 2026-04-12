using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.CreateBasket;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Application.BasketSummary;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Linq;
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
    private readonly BasketSummaryHandler _basketSummaryHandler;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<BasketFunction> _logger;

    public BasketFunction(
        CreateBasketHandler createBasketHandler,
        ConfirmBasketHandler confirmBasketHandler,
        BasketSummaryHandler basketSummaryHandler,
        OrderServiceClient orderServiceClient,
        ILogger<BasketFunction> logger)
    {
        _createBasketHandler = createBasketHandler;
        _confirmBasketHandler = confirmBasketHandler;
        _basketSummaryHandler = basketSummaryHandler;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/basket
    // -------------------------------------------------------------------------

    [Function("CreateBasket")]
    [OpenApiOperation(operationId: "CreateBasket", tags: new[] { "Basket" }, Summary = "Create a new shopping basket")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBasketRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateBasketResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/basket")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateBasketRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request!.Segments is null || request.Segments.Count == 0)
            return await req.BadRequestAsync("At least one 'segments' entry is required.");

        if (string.IsNullOrWhiteSpace(request.ChannelCode))
            return await req.BadRequestAsync("The field 'channelCode' is required.");

        if (request.PassengerCount < 1)
            return await req.BadRequestAsync("The field 'passengerCount' must be at least 1.");

        var command = new CreateBasketCommand(
            request.Segments.Select(s => new BasketSegment(s.OfferId, s.SessionId)).ToList(),
            request.ChannelCode,
            request.CurrencyCode,
            request.BookingType,
            request.LoyaltyNumber,
            request.CustomerId,
            request.PassengerCount);

        try
        {
            var result = await _createBasketHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync($"/v1/basket/{result.BasketId}",
                new CreateBasketResponse { BasketId = result.BasketId });
        }
        catch (InvalidOperationException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // GET /v1/basket/{basketId}
    // -------------------------------------------------------------------------

    [Function("GetBasket")]
    [OpenApiOperation(operationId: "GetBasket", tags: new[] { "Basket" }, Summary = "Get basket by ID")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/basket/{basketId:guid}")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        var raw = await _orderServiceClient.GetBasketRawAsync(basketId, cancellationToken);
        if (raw is null) return req.CreateResponse(HttpStatusCode.NotFound);

        var httpResponse = req.CreateResponse(HttpStatusCode.OK);
        httpResponse.Headers.Add("Content-Type", "application/json");
        await httpResponse.WriteStringAsync(raw, cancellationToken);
        return httpResponse;
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/flights
    // -------------------------------------------------------------------------

    [Function("UpdateBasketFlights")]
    [OpenApiOperation(operationId: "UpdateBasketFlights", tags: new[] { "Basket" }, Summary = "Update flight selections in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Flight offer to add")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateFlights(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/flights")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for UpdateFlights");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        await _orderServiceClient.AddOfferAsync(basketId, body, cancellationToken);

        var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
        if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(MapToBasketResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/passengers
    // -------------------------------------------------------------------------

    [Function("UpdateBasketPassengers")]
    [OpenApiOperation(operationId: "UpdateBasketPassengers", tags: new[] { "Basket" }, Summary = "Update passengers in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Passenger details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/passengers")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for UpdatePassengers");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        await _orderServiceClient.UpdatePassengersAsync(basketId, body, cancellationToken);

        var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
        if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(MapToBasketResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/seats
    // -------------------------------------------------------------------------

    [Function("UpdateBasketSeats")]
    [OpenApiOperation(operationId: "UpdateBasketSeats", tags: new[] { "Basket" }, Summary = "Update seat selections in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Seat selections")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/seats")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for UpdateSeats");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        await _orderServiceClient.UpdateSeatsAsync(basketId, body, cancellationToken);

        var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
        if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(MapToBasketResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/bags
    // -------------------------------------------------------------------------

    [Function("UpdateBasketBags")]
    [OpenApiOperation(operationId: "UpdateBasketBags", tags: new[] { "Basket" }, Summary = "Update bag selections in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Bag selections")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/bags")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for UpdateBags");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        await _orderServiceClient.UpdateBagsAsync(basketId, body, cancellationToken);

        var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
        if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(MapToBasketResponse(basket));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/basket/{basketId}/ssrs
    // -------------------------------------------------------------------------

    [Function("UpdateBasketSsrs")]
    [OpenApiOperation(operationId: "UpdateBasketSsrs", tags: new[] { "Basket" }, Summary = "Update SSR selections in basket")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "SSR selections")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSsrs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/basket/{basketId:guid}/ssrs")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for UpdateSsrs");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        await _orderServiceClient.UpdateSsrsAsync(basketId, body, cancellationToken);

        var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
        if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(MapToBasketResponse(basket));
    }

    // -------------------------------------------------------------------------
    // GET /v1/basket/{basketId}/summary
    // -------------------------------------------------------------------------

    [Function("BasketSummary")]
    [OpenApiOperation(operationId: "BasketSummary", tags: new[] { "Basket" }, Summary = "Reprice all offers in the basket and return a pricing summary with tax line breakdown")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketSummaryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Summary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/basket/{basketId:guid}/summary")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        var result = await _basketSummaryHandler.HandleAsync(new BasketSummaryQuery(basketId), cancellationToken);
        if (result is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(result);
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
        var (request, error) = await req.TryDeserializeBodyAsync<ConfirmBasketRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Payment?.Method))
            return await req.BadRequestAsync("The field 'payment.method' is required.");

        var command = new ConfirmBasketCommand(
            basketId,
            request.Payment.Method,
            request.Payment.CardNumber,
            request.Payment.ExpiryDate,
            request.Payment.Cvv,
            request.Payment.CardholderName,
            request.LoyaltyPointsToRedeem);

        try
        {
            var result = await _confirmBasketHandler.HandleAsync(command, cancellationToken);
            return await req.CreatedAsync($"/v1/basket/{basketId}/confirm", result);
        }
        catch (InvalidOperationException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
        catch (PaymentValidationException ex)
        {
            return await req.UnprocessableEntityAsync(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static BasketResponse MapToBasketResponse(OrderMsBasketResult basket)
    {
        var flights = new List<BasketFlight>();

        if (basket.BasketData.HasValue && basket.BasketData.Value.ValueKind == JsonValueKind.Object)
        {
            try
            {
                if (basket.BasketData.Value.TryGetProperty("flightOffers", out var offersEl) &&
                    offersEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var offer in offersEl.EnumerateArray())
                    {
                        flights.Add(new BasketFlight
                        {
                            OfferId = offer.TryGetProperty("offerId", out var v) && v.TryGetGuid(out var g) ? g : Guid.Empty,
                            InventoryId = offer.TryGetProperty("inventoryId", out v) && v.TryGetGuid(out var ig) ? ig : null,
                            AircraftType = offer.TryGetProperty("aircraftType", out v) ? v.GetString() : null,
                            BasketItemId = offer.TryGetProperty("basketItemId", out v) ? v.GetString() ?? string.Empty : string.Empty,
                            FlightNumber = offer.TryGetProperty("flightNumber", out v) ? v.GetString() ?? string.Empty : string.Empty,
                            Origin = offer.TryGetProperty("origin", out v) ? v.GetString() ?? string.Empty : string.Empty,
                            Destination = offer.TryGetProperty("destination", out v) ? v.GetString() ?? string.Empty : string.Empty,
                            DepartureDateTime = ParseOfferDateTime(offer, "departureDate", "departureTime"),
                            ArrivalDateTime = ParseOfferDateTime(offer, "departureDate", "arrivalTime"),
                            CabinCode = offer.TryGetProperty("cabinCode", out v) ? v.GetString() ?? string.Empty : string.Empty,
                            FareFamily = offer.TryGetProperty("fareFamily", out v) ? v.GetString() : null,
                            TotalAmount = offer.TryGetProperty("totalAmount", out v) ? v.GetDecimal() : 0m
                        });
                    }
                }
            }
            catch { /* Return response with empty flights if basketData is malformed */ }
        }

        return new BasketResponse
        {
            BasketId = basket.BasketId,
            Status = basket.BasketStatus,
            Flights = flights,
            TotalFareAmount = basket.TotalFareAmount,
            TotalSeatAmount = basket.TotalSeatAmount,
            TotalBagAmount = basket.TotalBagAmount,
            TotalPrice = basket.TotalAmount ?? 0m,
            Currency = basket.CurrencyCode,
            CreatedAt = basket.CreatedAt,
            ExpiresAt = basket.ExpiresAt
        };
    }

    private static DateTime ParseOfferDateTime(JsonElement offer, string dateKey, string timeKey)
    {
        var date = offer.TryGetProperty(dateKey, out var dv) ? dv.GetString() ?? string.Empty : string.Empty;
        var time = offer.TryGetProperty(timeKey, out var tv) ? tv.GetString() ?? string.Empty : string.Empty;
        return DateTime.TryParse($"{date}T{time}", out var dt) ? dt : default;
    }
}
