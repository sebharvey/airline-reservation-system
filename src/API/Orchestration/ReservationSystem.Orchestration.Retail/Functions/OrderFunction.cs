using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.GetOrder;
using ReservationSystem.Orchestration.Retail.Application.CancelOrder;
using ReservationSystem.Orchestration.Retail.Application.AddOrderBags;
using ReservationSystem.Orchestration.Retail.Application.UpdateOrderSeats;
using ReservationSystem.Orchestration.Retail.Application.ChangeOrder;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Requests;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for order management.
/// Orchestrates calls across Order, Seat, Bag, Delivery, Customer and Offer microservices.
/// </summary>
public sealed class OrderFunction
{
    private readonly GetOrderHandler _getOrderHandler;
    private readonly CancelOrderHandler _cancelOrderHandler;
    private readonly AddOrderBagsHandler _addOrderBagsHandler;
    private readonly UpdateOrderSeatsHandler _updateOrderSeatsHandler;
    private readonly ChangeOrderHandler _changeOrderHandler;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<OrderFunction> _logger;

    public OrderFunction(
        GetOrderHandler getOrderHandler,
        CancelOrderHandler cancelOrderHandler,
        AddOrderBagsHandler addOrderBagsHandler,
        UpdateOrderSeatsHandler updateOrderSeatsHandler,
        ChangeOrderHandler changeOrderHandler,
        OrderServiceClient orderServiceClient,
        ILogger<OrderFunction> logger)
    {
        _getOrderHandler = getOrderHandler;
        _cancelOrderHandler = cancelOrderHandler;
        _addOrderBagsHandler = addOrderBagsHandler;
        _updateOrderSeatsHandler = updateOrderSeatsHandler;
        _changeOrderHandler = changeOrderHandler;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/retrieve
    // -------------------------------------------------------------------------

    [Function("RetrieveOrder")]
    [OpenApiOperation(operationId: "RetrieveOrder", tags: new[] { "Orders" }, Summary = "Retrieve an order by booking reference and passenger name")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RetrieveOrderRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ManagedOrderResponse), Description = "OK")]
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
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ManagedOrderResponse), Description = "OK")]
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
    // PATCH /v1/orders/{bookingRef}/passengers
    // -------------------------------------------------------------------------

    [Function("UpdateOrderPassengers")]
    [OpenApiOperation(operationId: "UpdateOrderPassengers", tags: new[] { "Orders" }, Summary = "Update passenger details for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePassengers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/passengers")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch { return await req.BadRequestAsync("Failed to read request body."); }

        try
        {
            await _orderServiceClient.UpdateOrderPassengersAsync(bookingRef.ToUpperInvariant(), body, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (InvalidOperationException ex) { return await req.BadRequestAsync(ex.Message); }
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/seats
    // -------------------------------------------------------------------------

    [Function("UpdateOrderSeats")]
    [OpenApiOperation(operationId: "UpdateOrderSeats", tags: new[] { "Orders" }, Summary = "Update seat selections for an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(UpdateOrderSeatsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Order not mutable")]
    public async Task<HttpResponseData> UpdateSeats(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/seats")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var (body, error) = await req.TryDeserializeBodyAsync<UpdateOrderSeatsCommand>(_logger, cancellationToken);
        if (error is not null) return error;

        try
        {
            var result = await _updateOrderSeatsHandler.HandleAsync(
                bookingRef.ToUpperInvariant(), body!, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException) { return req.CreateResponse(HttpStatusCode.NotFound); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/{bookingRef}/bags
    // -------------------------------------------------------------------------

    [Function("AddOrderBags")]
    [OpenApiOperation(operationId: "AddOrderBags", tags: new[] { "Orders" }, Summary = "Add bag ancillaries on a confirmed order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AddOrderBagsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Card declined or offer invalid")]
    public async Task<HttpResponseData> AddBags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/{bookingRef}/bags")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var (body, error) = await req.TryDeserializeBodyAsync<AddOrderBagsCommand>(_logger, cancellationToken);
        if (error is not null) return error;

        if (body!.BagSelections.Count == 0)
            return await req.BadRequestAsync("At least one bag selection is required.");

        try
        {
            var result = await _addOrderBagsHandler.HandleAsync(
                bookingRef.ToUpperInvariant(), body, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException) { return req.CreateResponse(HttpStatusCode.NotFound); }
        catch (PaymentValidationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/orders/{bookingRef}/ssrs
    // -------------------------------------------------------------------------

    [Function("UpdateOrderSsrs")]
    [OpenApiOperation(operationId: "UpdateOrderSsrs", tags: new[] { "Orders" }, Summary = "Add or remove SSRs on an order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Unprocessable — within amendment cut-off window")]
    public async Task<HttpResponseData> UpdateSsrs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/orders/{bookingRef}/ssrs")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        string body;
        try { body = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken); }
        catch { return await req.BadRequestAsync("Failed to read request body."); }

        try
        {
            await _orderServiceClient.UpdateOrderSsrsAsync(bookingRef.ToUpperInvariant(), body, cancellationToken);
            return req.CreateResponse(HttpStatusCode.OK);
        }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/{bookingRef}/cancel
    // -------------------------------------------------------------------------

    [Function("CancelOrder")]
    [OpenApiOperation(operationId: "CancelOrder", tags: new[] { "Orders" }, Summary = "Cancel a confirmed order")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CancelOrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Already cancelled")]
    public async Task<HttpResponseData> Cancel(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/{bookingRef}/cancel")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _cancelOrderHandler.HandleAsync(bookingRef.ToUpperInvariant(), cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException) { return req.CreateResponse(HttpStatusCode.NotFound); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }

    // -------------------------------------------------------------------------
    // POST /v1/orders/{bookingRef}/change
    // -------------------------------------------------------------------------

    [Function("ChangeOrder")]
    [OpenApiOperation(operationId: "ChangeOrder", tags: new[] { "Orders" }, Summary = "Change a confirmed flight to a new itinerary")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ChangeOrderResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.UnprocessableEntity, Description = "Fare does not permit change or card declined")]
    public async Task<HttpResponseData> Change(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/orders/{bookingRef}/change")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var (body, error) = await req.TryDeserializeBodyAsync<ChangeOrderCommand>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(body!.NewOfferId))
            return await req.BadRequestAsync("'newOfferId' is required.");

        try
        {
            var result = await _changeOrderHandler.HandleAsync(
                bookingRef.ToUpperInvariant(), body, cancellationToken);
            return await req.OkJsonAsync(result);
        }
        catch (KeyNotFoundException) { return req.CreateResponse(HttpStatusCode.NotFound); }
        catch (PaymentValidationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
        catch (InvalidOperationException ex) { return await req.UnprocessableEntityAsync(ex.Message); }
    }
}
