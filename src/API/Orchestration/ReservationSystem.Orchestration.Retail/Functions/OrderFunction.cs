using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.GetOrder;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for order management.
/// Orchestrates calls across Order, Seat, Bag, and Delivery microservices.
/// </summary>
public sealed class OrderFunction
{
    private readonly GetOrderHandler _getOrderHandler;
    private readonly ILogger<OrderFunction> _logger;

    public OrderFunction(
        GetOrderHandler getOrderHandler,
        ILogger<OrderFunction> logger)
    {
        _getOrderHandler = getOrderHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/orders/{bookingRef}
    // -------------------------------------------------------------------------

    [Function("GetOrder")]
    public async Task<HttpResponseData> GetByBookingRef(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/orders/{bookingRef}")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var order = await _getOrderHandler.HandleAsync(new GetOrderQuery(bookingRef), cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(order);
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/seats
    // -------------------------------------------------------------------------

    [Function("UpdateOrderSeats")]
    public Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/seats")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/bags
    // -------------------------------------------------------------------------

    [Function("UpdateOrderBags")]
    public Task<HttpResponseData> UpdateBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/bags")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/cancel
    // -------------------------------------------------------------------------

    [Function("CancelOrder")]
    public Task<HttpResponseData> Cancel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/cancel")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
