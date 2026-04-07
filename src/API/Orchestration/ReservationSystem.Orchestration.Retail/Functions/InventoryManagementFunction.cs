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
using System.Text.Json;

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

        // Fetch full orders in parallel so we can resolve passenger names per hold.
        var orderTasks = bookingRefs
            .Where(kv => kv.Value != null)
            .Select(async kv => (kv.Key, await _orderServiceClient.GetOrderByRefAsync(kv.Value!, cancellationToken)));
        var orders = (await Task.WhenAll(orderTasks))
            .Where(r => r.Item2 != null)
            .ToDictionary(r => r.Key, r => r.Item2!);

        var enriched = holds.Select(h => new FlightInventoryHoldDto
        {
            HoldId           = h.HoldId,
            OrderId          = h.OrderId,
            PassengerId      = h.PassengerId,
            BookingReference = bookingRefs.GetValueOrDefault(h.OrderId),
            PassengerName    = ResolvePassengerName(orders.GetValueOrDefault(h.OrderId), inventoryId.ToString(), h.SeatNumber, h.PassengerId),
            CabinCode        = h.CabinCode,
            SeatNumber       = h.SeatNumber,
            Status           = h.Status,
            CreatedAt        = h.CreatedAt
        });

        return await req.OkJsonAsync(enriched);
    }

    /// <summary>
    /// Resolves the passenger name for a single hold row.
    /// Uses passengerId stored on the hold when available.
    /// Falls back to seat-assignment lookup for holds without a stored passengerId.
    /// </summary>
    private static string? ResolvePassengerName(OrderMsOrderResult? order, string inventoryId, string? seatNumber, string? passengerId)
    {
        if (order?.OrderData is not { } data) return null;
        try
        {
            // Build passengerId → full name map from dataLists.passengers.
            var paxById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (data.TryGetProperty("dataLists", out var dataLists) &&
                dataLists.TryGetProperty("passengers", out var passEl) &&
                passEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in passEl.EnumerateArray())
                {
                    var id      = p.TryGetProperty("passengerId", out var pid) ? pid.GetString() : null;
                    var given   = p.TryGetProperty("givenName",   out var g)   ? g.GetString()   : null;
                    var surname = p.TryGetProperty("surname",     out var s)   ? s.GetString()   : null;
                    if (id != null)
                        paxById[id] = $"{given} {surname}".Trim();
                }
            }

            // Direct lookup via the passengerId stored on the hold (preferred path).
            if (!string.IsNullOrEmpty(passengerId) && paxById.TryGetValue(passengerId, out var directName))
                return directName;

            // Seat-assignment lookup for holds created before PassengerId was stored.
            if (!string.IsNullOrEmpty(seatNumber) &&
                data.TryGetProperty("seatAssignments", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatsEl.EnumerateArray())
                {
                    var segId  = seat.TryGetProperty("segmentId",  out var sid) ? sid.GetString() : null;
                    var sn     = seat.TryGetProperty("seatNumber", out var s)   ? s.GetString()   : null;
                    var paxId  = seat.TryGetProperty("passengerId",out var pid) ? pid.GetString() : null;
                    if (string.Equals(segId, inventoryId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(sn, seatNumber, StringComparison.OrdinalIgnoreCase) &&
                        paxId != null && paxById.TryGetValue(paxId, out var name))
                    {
                        return name;
                    }
                }
            }

            return null;
        }
        catch { return null; }
    }
}
