using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Application.CancelOrder;
using ReservationSystem.Microservices.Order.Application.ChangeOrder;
using ReservationSystem.Microservices.Order.Application.CreateOrder;
using ReservationSystem.Microservices.Order.Application.GetOrder;
using ReservationSystem.Microservices.Order.Application.RebookOrder;
using ReservationSystem.Microservices.Order.Application.UpdateOrderBags;
using ReservationSystem.Microservices.Order.Application.UpdateOrderSeats;
using ReservationSystem.Microservices.Order.Application.UpdateOrderSsrs;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using ReservationSystem.Microservices.Order.Models.Mappers;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Functions;

public sealed class OrderFunction
{
    private readonly CreateOrderHandler _createOrderHandler;
    private readonly GetOrderHandler _getOrderHandler;
    private readonly UpdateOrderSeatsHandler _updateSeatsHandler;
    private readonly UpdateOrderBagsHandler _updateBagsHandler;
    private readonly UpdateOrderSsrsHandler _updateSsrsHandler;
    private readonly CancelOrderHandler _cancelOrderHandler;
    private readonly ChangeOrderHandler _changeOrderHandler;
    private readonly RebookOrderHandler _rebookOrderHandler;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderFunction> _logger;

    public OrderFunction(
        CreateOrderHandler createOrderHandler,
        GetOrderHandler getOrderHandler,
        UpdateOrderSeatsHandler updateSeatsHandler,
        UpdateOrderBagsHandler updateBagsHandler,
        UpdateOrderSsrsHandler updateSsrsHandler,
        CancelOrderHandler cancelOrderHandler,
        ChangeOrderHandler changeOrderHandler,
        RebookOrderHandler rebookOrderHandler,
        IOrderRepository orderRepository,
        ILogger<OrderFunction> logger)
    {
        _createOrderHandler = createOrderHandler;
        _getOrderHandler = getOrderHandler;
        _updateSeatsHandler = updateSeatsHandler;
        _updateBagsHandler = updateBagsHandler;
        _updateSsrsHandler = updateSsrsHandler;
        _cancelOrderHandler = cancelOrderHandler;
        _changeOrderHandler = changeOrderHandler;
        _rebookOrderHandler = rebookOrderHandler;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    // POST /v1/orders
    [Function("CreateOrder")]
    public async Task<HttpResponseData> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders")] HttpRequestData req,
        CancellationToken ct)
    {
        CreateOrderRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(
                req.Body, SharedJsonOptions.CamelCase, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateOrder request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || request.BasketId == Guid.Empty)
            return await req.BadRequestAsync("The field 'basketId' is required.");

        if (request.BookingType == "Reward" && string.IsNullOrWhiteSpace(request.RedemptionReference))
            return await req.BadRequestAsync("'redemptionReference' is required for Reward bookings.");

        try
        {
            var command = OrderMapper.ToCommand(request);
            var order = await _createOrderHandler.HandleAsync(command, ct);

            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Content-Type", "application/json");
            httpResponse.Headers.Add("Location", $"/v1/orders/{order.BookingReference}");
            await httpResponse.WriteStringAsync(JsonSerializer.Serialize(
                OrderMapper.ToCreateResponse(order), SharedJsonOptions.CamelCase));
            return httpResponse;
        }
        catch (KeyNotFoundException ex) { return await req.NotFoundAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // POST /v1/orders/retrieve
    [Function("RetrieveOrder")]
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

    // GET /v1/orders/{bookingRef}
    [Function("GetOrder")]
    public async Task<HttpResponseData> GetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/orders/{bookingRef}")] HttpRequestData req,
        string bookingRef, CancellationToken ct)
    {
        // Avoid routing conflicts with "retrieve" POST endpoint
        if (bookingRef == "retrieve")
            return req.CreateResponse(HttpStatusCode.MethodNotAllowed);

        var order = await _getOrderHandler.HandleAsync(new GetOrderQuery(bookingRef), ct);
        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);
        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // PATCH /v1/orders/{bookingRef}/passengers
    [Function("UpdateOrderPassengers")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/passengers")] HttpRequestData req,
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

    // PATCH /v1/orders/{bookingRef}/seats
    [Function("UpdateOrderSeats")]
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

    // PATCH /v1/orders/{bookingRef}/change
    [Function("ChangeOrder")]
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

    // PATCH /v1/orders/{bookingRef}/rebook
    [Function("RebookOrder")]
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

    // POST /v1/orders/{bookingRef}/checkin
    [Function("CheckIn")]
    public async Task<HttpResponseData> CheckIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/{bookingRef}/checkin")] HttpRequestData req,
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

            var checkinCount = 0;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("checkins", out var checkins))
                    checkinCount = checkins.GetArrayLength();
            }
            catch { }

            return await req.OkJsonAsync(new
            {
                bookingReference = bookingRef,
                checkedInPassengers = checkinCount
            });
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }
}
