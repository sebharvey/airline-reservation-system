using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.GetOrder;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
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
    // POST /v1/orders/retrieve
    // -------------------------------------------------------------------------

    [Function("RetrieveOrder")]
    [OpenApiOperation(operationId: "RetrieveOrder", tags: new[] { "Orders" }, Summary = "Retrieve an order by booking reference and passenger name")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RetrieveOrderRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> RetrieveOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/retrieve")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (body, error) = await req.TryDeserializeBodyAsync<RetrieveOrderRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(body!.BookingReference) || string.IsNullOrWhiteSpace(body.Surname))
            return await req.BadRequestAsync("'bookingReference' and 'surname' are required.");

        var order = await _getOrderHandler.HandleRetrieveAsync(
            body.BookingReference.ToUpperInvariant().Trim(),
            body.Surname.Trim(),
            cancellationToken);

        if (order is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(order);
    }

    // -------------------------------------------------------------------------
    // GET /v1/orders/{bookingRef}
    // -------------------------------------------------------------------------

    [Function("GetOrder")]
    [OpenApiOperation(operationId: "GetOrder", tags: new[] { "Orders" }, Summary = "Get an order by booking reference")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
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
    [OpenApiOperation(operationId: "UpdateOrderSeats", tags: new[] { "Orders" }, Summary = "Update seat selections for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
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
    [OpenApiOperation(operationId: "UpdateOrderBags", tags: new[] { "Orders" }, Summary = "Update bag selections for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
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
    [OpenApiOperation(operationId: "CancelOrder", tags: new[] { "Orders" }, Summary = "Cancel an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OrderResponse), Description = "OK")]
    public Task<HttpResponseData> Cancel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/cancel")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
