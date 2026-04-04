using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrders;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrderDetail;
using ReservationSystem.Orchestration.Retail.Application.GetAdminOrderTickets;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using System.Net;
using System.Text;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for staff-facing order search and detail.
/// All routes require a valid staff JWT token with the "User" role claim,
/// enforced by <see cref="ReservationSystem.Shared.Business.Middleware.TerminalAuthenticationMiddleware"/>.
/// Function names are prefixed with "Admin" to trigger staff middleware.
/// </summary>
public sealed class AdminOrderManagementFunction
{
    private readonly GetAdminOrdersHandler _getAdminOrdersHandler;
    private readonly GetAdminOrderDetailHandler _getAdminOrderDetailHandler;
    private readonly GetAdminOrderTicketsHandler _getAdminOrderTicketsHandler;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<AdminOrderManagementFunction> _logger;

    public AdminOrderManagementFunction(
        GetAdminOrdersHandler getAdminOrdersHandler,
        GetAdminOrderDetailHandler getAdminOrderDetailHandler,
        GetAdminOrderTicketsHandler getAdminOrderTicketsHandler,
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<AdminOrderManagementFunction> logger)
    {
        _getAdminOrdersHandler = getAdminOrdersHandler;
        _getAdminOrderDetailHandler = getAdminOrderDetailHandler;
        _getAdminOrderTicketsHandler = getAdminOrderTicketsHandler;
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/orders?limit=10
    // -------------------------------------------------------------------------

    [Function("AdminGetOrders")]
    [OpenApiOperation(operationId: "AdminGetOrders", tags: new[] { "Admin Orders" }, Summary = "List the most recently created orders (staff)")]
    [OpenApiParameter(name: "limit", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Number of orders to return (default 10, max 100)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<AdminOrderSummaryResponse>), Description = "OK")]
    public async Task<HttpResponseData> GetOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        if (!int.TryParse(queryParams["limit"], out var limit) || limit <= 0 || limit > 100)
            limit = 10;

        var result = await _getAdminOrdersHandler.HandleAsync(
            new GetAdminOrdersQuery(limit), cancellationToken);

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/orders/{bookingRef}
    // -------------------------------------------------------------------------

    [Function("AdminGetOrderByRef")]
    [OpenApiOperation(operationId: "AdminGetOrderByRef", tags: new[] { "Admin Orders" }, Summary = "Get full order detail by booking reference (staff)")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The 6-character booking reference (PNR)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminOrderDetailResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetOrderByRef(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders/{bookingRef}")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var result = await _getAdminOrderDetailHandler.HandleAsync(
            bookingRef.ToUpperInvariant(), cancellationToken);

        if (result is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/orders/{bookingRef}/tickets
    // -------------------------------------------------------------------------

    [Function("AdminGetOrderTickets")]
    [OpenApiOperation(operationId: "AdminGetOrderTickets", tags: new[] { "Admin Orders" }, Summary = "Get all tickets for an order by booking reference (staff)")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The 6-character booking reference (PNR)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<AdminTicketRecord>), Description = "OK")]
    public async Task<HttpResponseData> GetOrderTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders/{bookingRef}/tickets")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var result = await _getAdminOrderTicketsHandler.HandleAsync(
            bookingRef.ToUpperInvariant(), cancellationToken);

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/orders/{bookingRef}/debug
    // TODO: Remove — temporary debug endpoint
    // -------------------------------------------------------------------------

    [Function("AdminDebugGetOrder")]
    [OpenApiOperation(operationId: "AdminDebugGetOrder", tags: new[] { "Admin Orders" }, Summary = "[TEMP] Return raw Order database row by booking reference (staff)")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Raw Order row as JSON")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DebugGetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders/{bookingRef}/debug")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var raw = await _orderServiceClient.GetOrderDebugRawAsync(bookingRef.ToUpperInvariant(), cancellationToken);
        if (raw is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(raw, Encoding.UTF8);
        return response;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/orders/{bookingRef}/debug/tickets
    // TODO: Remove — temporary debug endpoint
    // -------------------------------------------------------------------------

    [Function("AdminDebugGetTickets")]
    [OpenApiOperation(operationId: "AdminDebugGetTickets", tags: new[] { "Admin Orders" }, Summary = "[TEMP] Return raw Ticket database rows by booking reference (staff)")]
    [OpenApiParameter(name: "bookingRef", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The booking reference")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object[]), Description = "Raw Ticket rows as JSON")]
    public async Task<HttpResponseData> DebugGetTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders/{bookingRef}/debug/tickets")] HttpRequestData req,
        string bookingRef,
        CancellationToken cancellationToken)
    {
        var raw = await _deliveryServiceClient.GetTicketsDebugRawAsync(
            bookingRef.ToUpperInvariant(), cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(raw ?? "[]", Encoding.UTF8);
        return response;
    }
}
