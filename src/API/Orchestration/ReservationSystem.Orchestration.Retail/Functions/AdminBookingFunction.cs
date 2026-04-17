using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.SearchFlights;
using ReservationSystem.Orchestration.Retail.Application.CreateBasket;
using ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;
using ReservationSystem.Orchestration.Retail.Application.PaymentSummary;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for the staff-facing booking flow (Contact Centre / Terminal app).
/// Mirrors the public retail search, basket, and confirm endpoints but enforces staff JWT auth
/// via <see cref="ReservationSystem.Shared.Business.Middleware.TerminalAuthenticationMiddleware"/>.
/// All function names are prefixed with "Admin" to trigger that middleware automatically.
/// </summary>
public sealed class AdminBookingFunction
{
    private readonly SearchFlightsHandler _searchHandler;
    private readonly CreateBasketHandler _createBasketHandler;
    private readonly ConfirmBasketHandler _confirmBasketHandler;
    private readonly PaymentSummaryHandler _paymentSummaryHandler;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ProductServiceClient _productServiceClient;
    private readonly ILogger<AdminBookingFunction> _logger;

    public AdminBookingFunction(
        SearchFlightsHandler searchHandler,
        CreateBasketHandler createBasketHandler,
        ConfirmBasketHandler confirmBasketHandler,
        PaymentSummaryHandler paymentSummaryHandler,
        OrderServiceClient orderServiceClient,
        ProductServiceClient productServiceClient,
        ILogger<AdminBookingFunction> logger)
    {
        _searchHandler = searchHandler;
        _createBasketHandler = createBasketHandler;
        _confirmBasketHandler = confirmBasketHandler;
        _paymentSummaryHandler = paymentSummaryHandler;
        _orderServiceClient = orderServiceClient;
        _productServiceClient = productServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/search/slice
    // -------------------------------------------------------------------------

    [Function("AdminSearchSlice")]
    [OpenApiOperation(operationId: "AdminSearchSlice", tags: new[] { "Admin Booking" }, Summary = "Search for available flights for a directional slice (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchSliceRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SliceSearchResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> SearchSlice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/search/slice")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SearchSliceRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.Origin)
            || string.IsNullOrWhiteSpace(request.Destination)
            || string.IsNullOrWhiteSpace(request.DepartureDate)
            || request.PaxCount < 1)
        {
            return await req.BadRequestAsync("The fields 'origin', 'destination', 'departureDate', and 'paxCount' are required.");
        }

        var command = new SearchFlightsCommand(
            request.Origin,
            request.Destination,
            request.DepartureDate,
            request.PaxCount,
            request.BookingType);

        var result = await _searchHandler.HandleAsync(command, cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/basket
    // -------------------------------------------------------------------------

    [Function("AdminCreateBasket")]
    [OpenApiOperation(operationId: "AdminCreateBasket", tags: new[] { "Admin Booking" }, Summary = "Create a new shopping basket (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateBasketRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "Created — full basket response")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> CreateBasket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/basket")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateBasketRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request!.Segments is null || request.Segments.Count == 0)
            return await req.BadRequestAsync("At least one 'segments' entry is required.");

        if (request.PassengerCount < 1)
            return await req.BadRequestAsync("The field 'passengerCount' must be at least 1.");

        var command = new CreateBasketCommand(
            request.Segments.Select(s => new BasketSegment(s.OfferId, s.SessionId)).ToList(),
            request.Currency,
            request.BookingType,
            request.LoyaltyNumber,
            request.CustomerId,
            request.PassengerCount);

        try
        {
            var result = await _createBasketHandler.HandleAsync(command, cancellationToken);

            var basket = await _orderServiceClient.GetBasketAsync(result.BasketId, cancellationToken);
            if (basket is null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            return await req.CreatedAsync($"/v1/admin/basket/{result.BasketId}", MapToBasketResponse(basket));
        }
        catch (InvalidOperationException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // PUT /v1/admin/basket/{basketId}/passengers
    // -------------------------------------------------------------------------

    [Function("AdminUpdateBasketPassengers")]
    [OpenApiOperation(operationId: "AdminUpdateBasketPassengers", tags: new[] { "Admin Booking" }, Summary = "Update passengers in basket (staff)")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Passenger details")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/basket/{basketId:guid}/passengers")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for AdminUpdateBasketPassengers");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        try
        {
            await _orderServiceClient.UpdatePassengersAsync(basketId, body, cancellationToken);
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (InvalidOperationException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/basket/{basketId}/confirm
    // -------------------------------------------------------------------------

    [Function("AdminConfirmBasket")]
    [OpenApiOperation(operationId: "AdminConfirmBasket", tags: new[] { "Admin Booking" }, Summary = "Confirm basket and process payment (staff)")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ConfirmBasketRequest), Required = true, Description = "Request body")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable Entity — payment declined")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> ConfirmBasket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/basket/{basketId:guid}/confirm")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ConfirmBasketRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.ChannelCode))
            return await req.BadRequestAsync("The field 'channelCode' is required.");

        if (string.IsNullOrWhiteSpace(request.Payment?.Method))
            return await req.BadRequestAsync("The field 'payment.method' is required.");

        var command = new ConfirmBasketCommand(
            basketId,
            request.ChannelCode,
            request.Payment.Method,
            request.Payment.CardNumber,
            request.Payment.ExpiryDate,
            request.Payment.Cvv,
            request.Payment.CardholderName,
            request.LoyaltyPointsToRedeem);

        try
        {
            var result = await _confirmBasketHandler.HandleAsync(command, cancellationToken);
            return await req.OkJsonAsync(result);
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
    // PUT /v1/admin/basket/{basketId}/seats
    // -------------------------------------------------------------------------

    [Function("AdminUpdateBasketSeats")]
    [OpenApiOperation(operationId: "AdminUpdateBasketSeats", tags: new[] { "Admin Booking" }, Summary = "Update seat selections in basket (staff)")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Seat selections")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> UpdateBasketSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/basket/{basketId:guid}/seats")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for AdminUpdateBasketSeats");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        try
        {
            await _orderServiceClient.UpdateSeatsAsync(basketId, body, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
        if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(MapToBasketResponse(basket));
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/basket/{basketId}/payment-summary
    // -------------------------------------------------------------------------

    [Function("AdminGetPaymentSummary")]
    [OpenApiOperation(operationId: "AdminGetPaymentSummary", tags: new[] { "Admin Booking" }, Summary = "Return payment-screen summary for a basket (staff)")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(PaymentSummaryResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> GetPaymentSummary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/basket/{basketId:guid}/payment-summary")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        var result = await _paymentSummaryHandler.HandleAsync(new PaymentSummaryQuery(basketId), cancellationToken);
        if (result is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/products
    // -------------------------------------------------------------------------

    [Function("AdminGetProducts")]
    [OpenApiOperation(operationId: "AdminGetProducts", tags: new[] { "Admin Booking" }, Summary = "List all active retail products grouped by product group (staff)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Products with prices grouped by product group")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> GetProducts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/products")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var productsTask = _productServiceClient.GetAllProductsAsync(cancellationToken);
        var groupsTask   = _productServiceClient.GetAllProductGroupsAsync(cancellationToken);

        await Task.WhenAll(productsTask, groupsTask);

        var productList = await productsTask;
        var groupList   = await groupsTask;

        if (productList is null || productList.Products.Count == 0)
            return await req.OkJsonAsync(new { productGroups = Array.Empty<object>() });

        var groupNames = groupList?.Groups
            .Where(g => g.IsActive)
            .ToDictionary(g => g.ProductGroupId, g => g.Name)
            ?? new Dictionary<Guid, string>();

        var grouped = productList.Products
            .Where(p => p.IsActive)
            .GroupBy(p => p.ProductGroupId)
            .Select(g => new
            {
                productGroupId   = g.Key,
                productGroupName = groupNames.TryGetValue(g.Key, out var gn) ? gn : string.Empty,
                products         = g.Select(p => new
                {
                    productId         = p.ProductId,
                    name              = p.Name,
                    description       = p.Description,
                    imageBase64       = p.ImageBase64,
                    ssrCode           = p.SsrCode,
                    isSegmentSpecific = p.IsSegmentSpecific,
                    prices            = p.Prices
                        .Where(pr => pr.IsActive)
                        .Select(pr => new
                        {
                            priceId      = pr.PriceId,
                            offerId      = pr.OfferId,
                            currencyCode = pr.CurrencyCode,
                            price        = pr.Price,
                            tax          = pr.Tax
                        })
                        .ToList()
                }).ToList()
            })
            .ToList();

        return await req.OkJsonAsync(new { productGroups = grouped });
    }

    // -------------------------------------------------------------------------
    // PUT /v1/admin/basket/{basketId}/products
    // -------------------------------------------------------------------------

    [Function("AdminUpdateBasketProducts")]
    [OpenApiOperation(operationId: "AdminUpdateBasketProducts", tags: new[] { "Admin Booking" }, Summary = "Update add-on product selections in basket (staff)")]
    [OpenApiParameter(name: "basketId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The basket identifier")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Product selections")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(BasketResponse), Description = "OK — updated basket")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized — staff JWT required")]
    public async Task<HttpResponseData> UpdateBasketProducts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/basket/{basketId:guid}/products")] HttpRequestData req,
        Guid basketId,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read request body for AdminUpdateBasketProducts");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        try
        {
            await _orderServiceClient.UpdateProductsAsync(basketId, body, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        var basket = await _orderServiceClient.GetBasketAsync(basketId, cancellationToken);
        if (basket is null) return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(MapToBasketResponse(basket));
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
                            FareAmount = offer.TryGetProperty("baseFareAmount", out v) ? v.GetDecimal() : 0m,
                            TaxAmount = offer.TryGetProperty("taxAmount", out v) ? v.GetDecimal() : 0m,
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
            TotalProductAmount = basket.TotalProductAmount,
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
