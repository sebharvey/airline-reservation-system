using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Microservices.Order.Application.CancelOrder;
using ReservationSystem.Microservices.Order.Application.ChangeOrder;
using ReservationSystem.Microservices.Order.Application.ConfirmOrder;
using ReservationSystem.Microservices.Order.Application.CreateOrder;
using ReservationSystem.Microservices.Order.Application.DeleteDraftOrder;
using ReservationSystem.Microservices.Order.Application.GetOrder;
using ReservationSystem.Microservices.Order.Application.RebookOrder;
using ReservationSystem.Microservices.Order.Application.UpdateOrderBags;
using ReservationSystem.Microservices.Order.Application.UpdateOrderCheckIn;
using ReservationSystem.Microservices.Order.Application.UpdateOrderETickets;
using ReservationSystem.Microservices.Order.Application.UpdateOrderPassengers;
using ReservationSystem.Microservices.Order.Application.UpdateOrderSeats;
using ReservationSystem.Microservices.Order.Application.UpdateOrderSsrs;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using ReservationSystem.Microservices.Order.Models.Mappers;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Microservices.Order.Models.Responses;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Functions;

public sealed class OrderFunction
{
    private readonly CreateOrderHandler _createOrderHandler;
    private readonly ConfirmOrderHandler _confirmOrderHandler;
    private readonly GetOrderHandler _getOrderHandler;
    private readonly UpdateOrderPassengersHandler _updatePassengersHandler;
    private readonly UpdateOrderSeatsHandler _updateSeatsHandler;
    private readonly UpdateOrderBagsHandler _updateBagsHandler;
    private readonly UpdateOrderSsrsHandler _updateSsrsHandler;
    private readonly UpdateOrderETicketsHandler _updateETicketsHandler;
    private readonly UpdateOrderCheckInHandler _updateCheckInHandler;
    private readonly CancelOrderHandler _cancelOrderHandler;
    private readonly ChangeOrderHandler _changeOrderHandler;
    private readonly RebookOrderHandler _rebookOrderHandler;
    private readonly DeleteDraftOrderHandler _deleteDraftOrderHandler;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderFunction> _logger;

    public OrderFunction(
        CreateOrderHandler createOrderHandler,
        ConfirmOrderHandler confirmOrderHandler,
        GetOrderHandler getOrderHandler,
        UpdateOrderPassengersHandler updatePassengersHandler,
        UpdateOrderSeatsHandler updateSeatsHandler,
        UpdateOrderBagsHandler updateBagsHandler,
        UpdateOrderSsrsHandler updateSsrsHandler,
        UpdateOrderETicketsHandler updateETicketsHandler,
        UpdateOrderCheckInHandler updateCheckInHandler,
        CancelOrderHandler cancelOrderHandler,
        ChangeOrderHandler changeOrderHandler,
        RebookOrderHandler rebookOrderHandler,
        DeleteDraftOrderHandler deleteDraftOrderHandler,
        IOrderRepository orderRepository,
        ILogger<OrderFunction> logger)
    {
        _createOrderHandler = createOrderHandler;
        _confirmOrderHandler = confirmOrderHandler;
        _getOrderHandler = getOrderHandler;
        _updatePassengersHandler = updatePassengersHandler;
        _updateSeatsHandler = updateSeatsHandler;
        _updateBagsHandler = updateBagsHandler;
        _updateSsrsHandler = updateSsrsHandler;
        _updateETicketsHandler = updateETicketsHandler;
        _updateCheckInHandler = updateCheckInHandler;
        _cancelOrderHandler = cancelOrderHandler;
        _changeOrderHandler = changeOrderHandler;
        _rebookOrderHandler = rebookOrderHandler;
        _deleteDraftOrderHandler = deleteDraftOrderHandler;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    // POST /v1/orders
    [Function("CreateOrder")]
    [OpenApiOperation(operationId: "CreateOrder", tags: new[] { "Orders" }, Summary = "Create a draft order from a basket")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateOrderRequest), Required = true, Description = "The order creation request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateOrderResponse), Description = "Created — order is in Draft status with no booking reference yet")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders")] HttpRequestData req,
        CancellationToken ct)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateOrderRequest>(_logger, ct);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.ChannelCode))
            return await req.BadRequestAsync("The field 'channelCode' is required.");

        var command = OrderMapper.ToCommand(request);
        var order = await _createOrderHandler.HandleAsync(command, ct);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Location", $"/v1/orders/{order.OrderId}");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(
            OrderMapper.ToCreateResponse(order), SharedJsonOptions.CamelCase));
        return httpResponse;
    }

    // POST /v1/orders/confirm
    [Function("ConfirmOrder")]
    [OpenApiOperation(operationId: "ConfirmOrder", tags: new[] { "Orders" }, Summary = "Confirm a draft order, assigning a booking reference (PNR)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ConfirmOrderRequest), Required = true, Description = "The order confirmation request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ConfirmOrderResponse), Description = "OK — order confirmed with booking reference")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable Entity")]
    public async Task<HttpResponseData> ConfirmOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/confirm")] HttpRequestData req,
        CancellationToken ct)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ConfirmOrderRequest>(_logger, ct);
        if (error is not null) return error;

        if (request!.OrderId == Guid.Empty)
            return await req.BadRequestAsync("The field 'orderId' is required.");

        if (request.BasketId == Guid.Empty)
            return await req.BadRequestAsync("The field 'basketId' is required.");

        try
        {
            var command = OrderMapper.ToConfirmCommand(request);
            var order = await _confirmOrderHandler.HandleAsync(command, ct);
            return await req.OkJsonAsync(OrderMapper.ToConfirmResponse(order));
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // POST /v1/orders/retrieve
    [Function("RetrieveOrder")]
    [OpenApiOperation(operationId: "RetrieveOrder", tags: new[] { "Orders" }, Summary = "Retrieve an order by booking reference and surname")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RetrieveOrderRequest), Required = true, Description = "The retrieval request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> RetrieveOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/retrieve")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        var bookingRef = body.GetProperty("bookingReference").GetString()!;
        var surname = body.GetProperty("surname").GetString()!;

        var order = await _getOrderHandler.HandleAsync(new GetOrderQuery(bookingRef), ct);
        if (order is null)
            return await req.NotFoundAsync($"No order found for booking reference {bookingRef}.");

        // Validate surname match
        try
        {
            using var doc = JsonDocument.Parse(order.OrderData);
            var hasMatch = false;
            if (doc.RootElement.TryGetProperty("dataLists", out var dataLists) &&
                dataLists.TryGetProperty("passengers", out var passengers))
            {
                foreach (var pax in passengers.EnumerateArray())
                {
                    if (pax.TryGetProperty("surname", out var sn) &&
                        string.Equals(sn.GetString(), surname, StringComparison.OrdinalIgnoreCase))
                    {
                        hasMatch = true;
                        break;
                    }
                }
            }
            if (!hasMatch)
                return await req.NotFoundAsync($"No order found for booking reference {bookingRef} and surname {surname}.");
        }
        catch { }

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // GET /v1/orders
    [Function("QueryOrders")]
    [OpenApiOperation(operationId: "QueryOrders", tags: new[] { "Orders" }, Summary = "Query orders for a specific flight")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The flight number")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The departure date")]
    [OpenApiParameter(name: "status", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The order status filter")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse[]), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> QueryOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/orders")] HttpRequestData req,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var flightNumber = query["flightNumber"];
        var departureDate = query["departureDate"];
        var status = query["status"];

        if (string.IsNullOrWhiteSpace(flightNumber) || string.IsNullOrWhiteSpace(departureDate))
            return await req.BadRequestAsync("Query parameters 'flightNumber' and 'departureDate' are required.");

        var orders = await _orderRepository.GetByFlightAsync(flightNumber, departureDate, status, ct);

        if (orders.Count == 0)
            return await req.NotFoundAsync($"No orders found for flight {flightNumber} on {departureDate}.");

        return await req.OkJsonAsync(new
        {
            flightNumber,
            departureDate,
            totalOrders = orders.Count,
            orders = orders.Select(o => OrderMapper.ToResponse(o))
        });
    }

    // GET /v1/orders/irops
    [Function("GetIropsOrders")]
    [OpenApiOperation(operationId: "GetIropsOrders", tags: new[] { "Orders" }, Summary = "Get all confirmed orders on a flight, projected for IROPS processing")]
    [OpenApiParameter(name: "flightNumber", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The flight number")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "The departure date (yyyy-MM-dd)")]
    [OpenApiParameter(name: "status", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Order status filter")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> GetIropsOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/orders/irops")] HttpRequestData req,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var flightNumber = query["flightNumber"];
        var departureDate = query["departureDate"];
        var status = query["status"];

        if (string.IsNullOrWhiteSpace(flightNumber) || string.IsNullOrWhiteSpace(departureDate))
            return await req.BadRequestAsync("Query parameters 'flightNumber' and 'departureDate' are required.");

        var orders = await _orderRepository.GetByFlightAsync(flightNumber, departureDate, status, ct);

        var projected = orders
            .Select(o => ProjectToIropsDto(o, flightNumber, departureDate))
            .Where(dto => dto is not null)
            .ToList();

        return await req.OkJsonAsync(new
        {
            count = projected.Count,
            orders = projected
        });
    }

    // POST /v1/orders/irops
    [Function("GetIropsOrdersByIds")]
    [OpenApiOperation(operationId: "GetIropsOrdersByIds", tags: new[] { "Orders" }, Summary = "Batch-fetch specific orders projected for IROPS processing, identified by OrderIds from the flight manifest")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "{ orderIds: [guid, ...], flightNumber: string, departureDate: string }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> GetIropsOrdersByIds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/irops")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("orderIds", out var orderIdsEl) || orderIdsEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'orderIds' array is required.");

        if (!body.TryGetProperty("flightNumber", out var fnEl) || string.IsNullOrWhiteSpace(fnEl.GetString()))
            return await req.BadRequestAsync("'flightNumber' is required.");

        if (!body.TryGetProperty("departureDate", out var ddEl) || string.IsNullOrWhiteSpace(ddEl.GetString()))
            return await req.BadRequestAsync("'departureDate' is required.");

        var orderIds = new List<Guid>();
        foreach (var el in orderIdsEl.EnumerateArray())
        {
            if (!el.TryGetGuid(out var id)) return await req.BadRequestAsync("Each orderId must be a valid GUID.");
            orderIds.Add(id);
        }

        var flightNumber = fnEl.GetString()!;
        var departureDate = ddEl.GetString()!;

        var orders = await _orderRepository.GetByIdsAsync(orderIds, ct);

        var projected = orders
            .Where(o => o.OrderStatus == "Confirmed")
            .Select(o => ProjectToIropsDto(o, flightNumber, departureDate))
            .Where(dto => dto is not null)
            .ToList();

        return await req.OkJsonAsync(new
        {
            count = projected.Count,
            orders = projected
        });
    }

    private static object? ProjectToIropsDto(
        Domain.Entities.Order order,
        string flightNumber,
        string departureDate)
    {
        if (string.IsNullOrEmpty(order.BookingReference)) return null;

        try
        {
            using var doc = JsonDocument.Parse(order.OrderData);
            var root = doc.RootElement;

            var bookingType = root.TryGetProperty("bookingType", out var bt)
                ? bt.GetString() ?? "Revenue" : "Revenue";

            // Loyalty number from first passenger
            string? loyaltyNumber = null;
            if (root.TryGetProperty("dataLists", out var dl) &&
                dl.TryGetProperty("passengers", out var paxList) &&
                paxList.ValueKind == JsonValueKind.Array)
            {
                foreach (var pax in paxList.EnumerateArray())
                {
                    if (pax.TryGetProperty("loyaltyNumber", out var ln) &&
                        ln.ValueKind != JsonValueKind.Null)
                    {
                        loyaltyNumber = ln.GetString();
                        break;
                    }
                }
            }

            // Points for reward bookings
            var totalPointsAmount = 0;
            if (root.TryGetProperty("pointsRedemption", out var pr) &&
                pr.TryGetProperty("totalPointsAmount", out var tpa))
                tpa.TryGetInt32(out totalPointsAmount);

            // Payment reference for refunds
            string? originalPaymentId = null;
            if (root.TryGetProperty("payments", out var payments) &&
                payments.ValueKind == JsonValueKind.Array)
            {
                foreach (var payment in payments.EnumerateArray())
                {
                    if (payment.TryGetProperty("paymentReference", out var payRef) &&
                        payRef.ValueKind != JsonValueKind.Null)
                    {
                        originalPaymentId = payRef.GetString();
                        break;
                    }
                }
            }

            // Find the FLIGHT order item matching the cancelled flight
            object? segment = null;
            if (root.TryGetProperty("orderItems", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("productType", out var pt) &&
                        pt.GetString() != "FLIGHT") continue;

                    var fn = item.TryGetProperty("flightNumber", out var fnEl) ? fnEl.GetString() : null;
                    var dd = item.TryGetProperty("departureDate", out var ddEl) ? ddEl.GetString() : null;

                    if (fn != flightNumber || dd != departureDate) continue;

                    var inventoryIdStr = item.TryGetProperty("inventoryId", out var invEl)
                        ? invEl.GetString() : null;
                    Guid.TryParse(inventoryIdStr, out var inventoryId);

                    segment = new
                    {
                        segmentId = item.TryGetProperty("offerId", out var oid) ? oid.GetString() ?? "" : "",
                        inventoryId,
                        flightNumber = fn,
                        departureDate = dd,
                        cabinCode = item.TryGetProperty("cabinCode", out var cc) ? cc.GetString() ?? "" : "",
                        origin = item.TryGetProperty("origin", out var org) ? org.GetString() ?? "" : "",
                        destination = item.TryGetProperty("destination", out var dest) ? dest.GetString() ?? "" : ""
                    };
                    break;
                }
            }

            if (segment is null) return null;

            // Passengers
            var passengers = new List<object>();
            if (root.TryGetProperty("dataLists", out var dl2) &&
                dl2.TryGetProperty("passengers", out var paxArr) &&
                paxArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var pax in paxArr.EnumerateArray())
                {
                    passengers.Add(new
                    {
                        passengerId = pax.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "",
                        givenName = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "",
                        surname = pax.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "",
                        passengerType = pax.TryGetProperty("passengerType", out var ptype)
                            ? ptype.GetString() ?? "ADT" : "ADT",
                        eTicketNumbers = Array.Empty<string>()
                    });
                }
            }

            return new
            {
                orderId = order.OrderId,
                bookingReference = order.BookingReference,
                bookingType,
                loyaltyNumber,
                loyaltyTier = (string?)null,
                bookingDate = order.CreatedAt,
                totalPaid = order.TotalAmount ?? 0m,
                totalPointsAmount,
                originalPaymentId,
                currencyCode = order.CurrencyCode,
                segment,
                passengers
            };
        }
        catch
        {
            return null;
        }
    }

    // GET /v1/orders/{bookingRef}
    [Function("GetOrder")]
    [OpenApiOperation(operationId: "GetOrder", tags: new[] { "Orders" }, Summary = "Get an order by booking reference")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/orders/{bookingRef}")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        // Avoid routing conflicts with static POST sub-routes
        if (bookingRef is "retrieve" or "confirm" or "irops")
            return req.CreateResponse(HttpStatusCode.MethodNotAllowed);

        var order = await _getOrderHandler.HandleAsync(new GetOrderQuery(bookingRef), ct);
        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // PATCH /v1/orders/{bookingRef}/passengers
    [Function("UpdateOrderPassengers")]
    [OpenApiOperation(operationId: "UpdateOrderPassengers", tags: new[] { "Orders" }, Summary = "Update passenger details for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The passenger update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/passengers")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateOrderPassengersCommand(bookingRef.ToUpperInvariant(), body);
        try
        {
            var order = await _updatePassengersHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/orders/{bookingRef}/seats
    [Function("UpdateOrderSeats")]
    [OpenApiOperation(operationId: "UpdateOrderSeats", tags: new[] { "Orders" }, Summary = "Update seat selections for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The seat update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/seats")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateOrderSeatsCommand(bookingRef, body);
        try
        {
            var order = await _updateSeatsHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/orders/{bookingRef}/segments
    [Function("UpdateOrderSegments")]
    [OpenApiOperation(operationId: "UpdateOrderSegments", tags: new[] { "Orders" }, Summary = "Update flight segments for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The segment update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSegments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/segments")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new ChangeOrderCommand(bookingRef, body);
        try
        {
            var order = await _changeOrderHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/orders/{bookingRef}/bags
    [Function("UpdateOrderBags")]
    [OpenApiOperation(operationId: "UpdateOrderBags", tags: new[] { "Orders" }, Summary = "Update bag selections for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The bag update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/bags")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateOrderBagsCommand(bookingRef, body);
        try
        {
            var order = await _updateBagsHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/orders/{bookingRef}/ssrs
    [Function("UpdateOrderSsrs")]
    [OpenApiOperation(operationId: "UpdateOrderSsrs", tags: new[] { "Orders" }, Summary = "Update SSR selections for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The SSR update request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateSsrs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/ssrs")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateOrderSsrsCommand(bookingRef, body);
        try
        {
            var order = await _updateSsrsHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/orders/{bookingRef}/tickets
    [Function("UpdateOrderETickets")]
    [OpenApiOperation(operationId: "UpdateOrderETickets", tags: new[] { "Orders" }, Summary = "Write issued e-ticket numbers to an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "Array of issued e-tickets")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateETickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/tickets")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new UpdateOrderETicketsCommand(bookingRef, body);
        try
        {
            var order = await _updateETicketsHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/orders/{bookingRef}/change
    [Function("ChangeOrder")]
    [OpenApiOperation(operationId: "ChangeOrder", tags: new[] { "Orders" }, Summary = "Apply changes to an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The change request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> ChangeOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/change")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new ChangeOrderCommand(bookingRef, body);
        try
        {
            var order = await _changeOrderHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // PATCH /v1/orders/{bookingRef}/cancel
    [Function("CancelOrder")]
    [OpenApiOperation(operationId: "CancelOrder", tags: new[] { "Orders" }, Summary = "Cancel an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The cancel request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> CancelOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/cancel")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new CancelOrderCommand(bookingRef, body);
        try
        {
            var order = await _cancelOrderHandler.HandleAsync(command, ct);
            if (order is null) return await req.NotFoundAsync($"Order '{bookingRef}' not found.");
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException) { return await req.UnprocessableEntityAsync($"Order '{bookingRef}' is already cancelled."); }
    }

    // PATCH /v1/orders/{bookingRef}/rebook
    [Function("RebookOrder")]
    [OpenApiOperation(operationId: "RebookOrder", tags: new[] { "Orders" }, Summary = "Rebook an order onto a new flight")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "The rebook request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderStatusResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> RebookOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/rebook")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read body"); return await req.BadRequestAsync("Failed to read request body."); }

        var command = new RebookOrderCommand(bookingRef, body);
        try
        {
            var order = await _rebookOrderHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                orderStatus = order.OrderStatus,
                version = order.Version
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // GET /v1/debug/orders/{bookingRef}
    // TODO: Remove this endpoint — temporary debug only
    [Function("DebugGetOrder")]
    [OpenApiOperation(operationId: "DebugGetOrder", tags: new[] { "Debug" }, Summary = "[TEMP] Return raw Order database row by booking reference")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Raw Order row as JSON")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DebugGetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/debug/orders/{bookingRef}")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        var order = await _orderRepository.GetByBookingReferenceAsync(bookingRef.ToUpperInvariant(), ct);
        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        JsonElement orderDataElement;
        try { orderDataElement = JsonSerializer.Deserialize<JsonElement>(order.OrderData); }
        catch { orderDataElement = JsonSerializer.Deserialize<JsonElement>("{}"); }

        return await req.OkJsonAsync(new
        {
            orderId = order.OrderId,
            bookingReference = order.BookingReference,
            orderStatus = order.OrderStatus,
            channelCode = order.ChannelCode,
            currency = order.CurrencyCode,
            ticketingTimeLimit = order.TicketingTimeLimit,
            totalAmount = order.TotalAmount,
            version = order.Version,
            createdAt = order.CreatedAt,
            updatedAt = order.UpdatedAt,
            orderData = orderDataElement
        });
    }

    // POST /v1/admin/orders/booking-references
    [Function("GetOrderBookingReferences")]
    [OpenApiOperation(operationId: "GetOrderBookingReferences", tags: new[] { "Admin Orders" }, Summary = "Batch-resolve booking references for a list of order IDs")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true, Description = "{ orderIds: [guid, ...] }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "OK — array of { orderId, bookingReference } pairs")]
    public async Task<HttpResponseData> GetOrderBookingReferences(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/orders/booking-references")] HttpRequestData req,
        CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("orderIds", out var orderIdsEl) || orderIdsEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'orderIds' array is required.");

        var orderIds = new List<Guid>();
        foreach (var el in orderIdsEl.EnumerateArray())
        {
            if (!el.TryGetGuid(out var id)) return await req.BadRequestAsync("Each orderId must be a valid GUID.");
            orderIds.Add(id);
        }

        var refs = await _orderRepository.GetBookingReferencesByIdsAsync(orderIds, ct);
        return await req.OkJsonAsync(refs.Select(kvp => new { orderId = kvp.Key, bookingReference = kvp.Value }));
    }

    // GET /v1/admin/orders?limit=10
    [Function("GetRecentOrders")]
    [OpenApiOperation(operationId: "GetRecentOrders", tags: new[] { "Admin Orders" }, Summary = "Get the most recently created orders")]
    [OpenApiParameter(name: "limit", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Number of orders to return (default 10, max 100)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse[]), Description = "OK")]
    public async Task<HttpResponseData> GetRecentOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders")] HttpRequestData req,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        if (!int.TryParse(query["limit"], out var limit) || limit <= 0 || limit > 100)
            limit = 10;

        var orders = await _orderRepository.GetRecentAsync(limit, ct);
        return await req.OkJsonAsync(orders.Select(o => OrderMapper.ToResponse(o)));
    }

    // PATCH /v1/orders/{bookingRef}/checkin
    [Function("CheckIn")]
    [OpenApiOperation(operationId: "CheckIn", tags: new[] { "Orders" }, Summary = "Write check-in status onto the orderItems for a departure airport")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(object), Required = true,
        Description = "{ departureAirport, checkedInAt, passengers: [{ passengerId, ticketNumber, status, message }] }")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CheckInResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> CheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/checkin")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body, SharedJsonOptions.CamelCase, ct); }
        catch (JsonException) { return await req.BadRequestAsync("Invalid JSON."); }

        if (!body.TryGetProperty("departureAirport", out var airportEl) || string.IsNullOrWhiteSpace(airportEl.GetString()))
            return await req.BadRequestAsync("'departureAirport' is required.");

        if (!body.TryGetProperty("passengers", out var paxEl) || paxEl.ValueKind != JsonValueKind.Array)
            return await req.BadRequestAsync("'passengers' array is required.");

        var departureAirport = airportEl.GetString()!.ToUpperInvariant().Trim();
        var checkedInAt = body.TryGetProperty("checkedInAt", out var tsEl) ? tsEl.GetString() ?? DateTime.UtcNow.ToString("o") : DateTime.UtcNow.ToString("o");

        var passengers = new List<UpdateOrderCheckInPassenger>();
        foreach (var p in paxEl.EnumerateArray())
        {
            var passengerId = p.TryGetProperty("passengerId", out var pidEl) ? pidEl.GetString() ?? "" : "";
            var ticketNumber = p.TryGetProperty("ticketNumber", out var tnEl) ? tnEl.GetString() ?? "" : "";
            var status = p.TryGetProperty("status", out var stEl) ? stEl.GetString() ?? "CheckedIn" : "CheckedIn";
            var message = p.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "" : "";
            passengers.Add(new UpdateOrderCheckInPassenger(passengerId, ticketNumber, status, message));
        }

        var command = new UpdateOrderCheckInCommand(
            bookingRef.ToUpperInvariant().Trim(),
            departureAirport,
            checkedInAt,
            passengers);

        try
        {
            var order = await _updateCheckInHandler.HandleAsync(command, ct);
            if (order is null) return req.CreateResponse(HttpStatusCode.NotFound);
            return await req.OkJsonAsync(new
            {
                bookingReference = order.BookingReference,
                checkedInPassengers = passengers.Count
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // DELETE /v1/orders/{orderId}
    [Function("DeleteDraftOrder")]
    [OpenApiOperation(operationId: "DeleteDraftOrder", tags: new[] { "Orders" }, Summary = "Delete a draft order by ID — only Draft orders may be deleted")]
    [OpenApiParameter(name: "orderId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "The draft order ID")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "No Content — order deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — order does not exist or is not in Draft status")]
    public async Task<HttpResponseData> DeleteDraftOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/orders/{orderId}")] HttpRequestData req,
        string orderId, CancellationToken ct)
    {
        if (!Guid.TryParse(orderId, out var orderGuid))
            return await req.BadRequestAsync("'orderId' must be a valid GUID.");

        var command = new DeleteDraftOrderCommand(orderGuid);
        var deleted = await _deleteDraftOrderHandler.HandleAsync(command, ct);

        return deleted
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }
}
