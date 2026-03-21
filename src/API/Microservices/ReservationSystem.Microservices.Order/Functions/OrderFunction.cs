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
using ReservationSystem.Microservices.Order.Models.Mappers;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Functions;

/// <summary>
/// HTTP-triggered functions for Order operations.
/// Presentation layer — translates HTTP concerns into application-layer calls.
/// </summary>
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
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders
    // -------------------------------------------------------------------------

    [Function("CreateOrder")]
    public async Task<HttpResponseData> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateOrderRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateOrder request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null || request.BasketId == Guid.Empty)
            return await req.BadRequestAsync("The field 'basketId' is required.");

        var command = OrderMapper.ToCommand(request);
        var order = await _createOrderHandler.HandleAsync(command, cancellationToken);
        var response = OrderMapper.ToCreateResponse(order);

        var httpResponse = req.CreateResponse(HttpStatusCode.Created);
        httpResponse.Headers.Add("Content-Type", "application/json");
        httpResponse.Headers.Add("Location", $"/v1/orders/{order.BookingReference}");
        await httpResponse.WriteStringAsync(JsonSerializer.Serialize(response, SharedJsonOptions.CamelCase));
        return httpResponse;
    }

    // -------------------------------------------------------------------------
    // GET /v1/orders/{bookingRef}
    // -------------------------------------------------------------------------

    [Function("GetOrder")]
    public async Task<HttpResponseData> GetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/orders/{bookingRef}")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var order = await _getOrderHandler.HandleAsync(new GetOrderQuery(bookingRef), cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/seats
    // -------------------------------------------------------------------------

    [Function("UpdateOrderSeats")]
    public async Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/seats")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        string seatsData;

        try
        {
            seatsData = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read UpdateOrderSeats request body");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        var command = new UpdateOrderSeatsCommand(bookingRef, seatsData);
        var order = await _updateSeatsHandler.HandleAsync(command, cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/bags
    // -------------------------------------------------------------------------

    [Function("UpdateOrderBags")]
    public async Task<HttpResponseData> UpdateBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/bags")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        string bagsData;

        try
        {
            bagsData = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read UpdateOrderBags request body");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        var command = new UpdateOrderBagsCommand(bookingRef, bagsData);
        var order = await _updateBagsHandler.HandleAsync(command, cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/ssrs
    // -------------------------------------------------------------------------

    [Function("UpdateOrderSsrs")]
    public async Task<HttpResponseData> UpdateSsrs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/ssrs")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        string ssrsData;

        try
        {
            ssrsData = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read UpdateOrderSsrs request body");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        var command = new UpdateOrderSsrsCommand(bookingRef, ssrsData);
        var order = await _updateSsrsHandler.HandleAsync(command, cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/cancel
    // -------------------------------------------------------------------------

    [Function("CancelOrder")]
    public async Task<HttpResponseData> CancelOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/cancel")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var cancelled = await _cancelOrderHandler.HandleAsync(new CancelOrderCommand(bookingRef), cancellationToken);

        return cancelled
            ? req.CreateResponse(HttpStatusCode.NoContent)
            : req.CreateResponse(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/change
    // -------------------------------------------------------------------------

    [Function("ChangeOrder")]
    public async Task<HttpResponseData> ChangeOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/change")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        string changeData;

        try
        {
            changeData = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read ChangeOrder request body");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        var command = new ChangeOrderCommand(bookingRef, changeData);
        var order = await _changeOrderHandler.HandleAsync(command, cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/rebook
    // -------------------------------------------------------------------------

    [Function("RebookOrder")]
    public async Task<HttpResponseData> RebookOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/rebook")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        string rebookData;

        try
        {
            rebookData = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read RebookOrder request body");
            return await req.BadRequestAsync("Failed to read request body.");
        }

        var command = new RebookOrderCommand(bookingRef, rebookData);
        var order = await _rebookOrderHandler.HandleAsync(command, cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(OrderMapper.ToResponse(order));
    }
}
