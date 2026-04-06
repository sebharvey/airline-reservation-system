using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Orchestration.Retail.Application.GetFlightInventory;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using System.Net;

namespace ReservationSystem.Orchestration.Retail.Functions;

/// <summary>
/// HTTP-triggered functions for staff-facing flight inventory management.
/// All routes require a valid staff JWT token with the "User" role claim,
/// enforced by <see cref="ReservationSystem.Shared.Business.Middleware.TerminalAuthenticationMiddleware"/>.
/// Function names are prefixed with "Admin" to trigger staff middleware.
/// </summary>
public sealed class InventoryManagementFunction
{
    private readonly GetFlightInventoryHandler _getFlightInventoryHandler;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<InventoryManagementFunction> _logger;

    public InventoryManagementFunction(
        GetFlightInventoryHandler getFlightInventoryHandler,
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient,
        ILogger<InventoryManagementFunction> logger)
    {
        _getFlightInventoryHandler = getFlightInventoryHandler;
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/inventory?departureDate=yyyy-MM-dd
    // -------------------------------------------------------------------------

    [Function("AdminGetFlightInventory")]
    [OpenApiOperation(operationId: "AdminGetFlightInventory", tags: new[] { "Admin Inventory" }, Summary = "Get flight inventory grouped by cabin for a given departure date (staff)")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Departure date (yyyy-MM-dd). Defaults to today.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FlightInventoryGroupDto>), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> GetFlightInventory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/inventory")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var dateParam = System.Web.HttpUtility.ParseQueryString(req.Url.Query)["departureDate"];

        DateOnly departureDate;
        if (string.IsNullOrWhiteSpace(dateParam))
        {
            departureDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!DateOnly.TryParseExact(dateParam, "yyyy-MM-dd", out departureDate))
        {
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");
        }

        var result = await _getFlightInventoryHandler.HandleAsync(
            new GetFlightInventoryQuery(departureDate), cancellationToken);

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/inventory/{inventoryId}/holds
    // -------------------------------------------------------------------------

    [Function("AdminGetInventoryHolds")]
    [OpenApiOperation(operationId: "AdminGetInventoryHolds", tags: new[] { "Admin Inventory" }, Summary = "Get all holds for a specific flight inventory record (staff)")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<FlightInventoryHoldDto>), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetInventoryHolds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/inventory/{inventoryId:guid}/holds")] HttpRequestData req,
        Guid inventoryId,
        CancellationToken cancellationToken)
    {
        var holds = await _offerServiceClient.GetInventoryHoldsAsync(inventoryId, cancellationToken);

        var orderIds = holds.Select(h => h.OrderId).Distinct().ToList();
        var bookingRefs = await _orderServiceClient.GetBookingReferencesAsync(orderIds, cancellationToken);

        var enriched = holds.Select(h => new FlightInventoryHoldDto
        {
            HoldId           = h.HoldId,
            OrderId          = h.OrderId,
            BookingReference = bookingRefs.GetValueOrDefault(h.OrderId),
            CabinCode        = h.CabinCode,
            SeatNumber       = h.SeatNumber,
            Status           = h.Status,
            CreatedAt        = h.CreatedAt
        });

        return await req.OkJsonAsync(enriched);
    }
}
