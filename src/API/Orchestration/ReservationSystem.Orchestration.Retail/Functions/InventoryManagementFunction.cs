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
using System.Text.Json.Nodes;

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
    [OpenApiOperation(operationId: "AdminGetFlightInventory", tags: new[] { "Admin Inventory" }, Summary = "Get flight inventory grouped by cabin for a given departure date, with optional pinned flights (staff)")]
    [OpenApiParameter(name: "departureDate", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Departure date (yyyy-MM-dd). Defaults to today.")]
    [OpenApiParameter(name: "pinnedInventoryIds", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "Comma-separated inventory IDs to include as pinned flights regardless of departure date.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(FlightInventoryWithPinnedDto), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> GetFlightInventory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/inventory")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var dateParam = qs["departureDate"];

        DateOnly departureDate;
        if (string.IsNullOrWhiteSpace(dateParam))
        {
            departureDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!DateOnly.TryParseExact(dateParam, "yyyy-MM-dd", out departureDate))
        {
            return await req.BadRequestAsync("'departureDate' must be in yyyy-MM-dd format.");
        }

        var pinnedIds = ParseGuidList(qs["pinnedInventoryIds"]);

        var result = await _getFlightInventoryHandler.HandleAsync(
            new GetFlightInventoryQuery(departureDate, pinnedIds), cancellationToken);

        return await req.OkJsonAsync(result);
    }

    private static IReadOnlyList<Guid> ParseGuidList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? (Guid?)g : null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList()
            .AsReadOnly();
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
            HoldType         = h.HoldType,
            StandbyPriority  = h.StandbyPriority,
            PaxCount         = h.PaxCount,
            CreatedAt        = h.CreatedAt
        });

        return await req.OkJsonAsync(enriched);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/inventory/{inventoryId}/inventory-orders
    // -------------------------------------------------------------------------

    [Function("AdminGetInventoryOrders")]
    [OpenApiOperation(operationId: "AdminGetInventoryOrders", tags: new[] { "Admin Inventory" }, Summary = "Get all fares and ancillaries sold for a flight (staff)")]
    [OpenApiParameter(name: "inventoryId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Flight inventory ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(InventoryOrdersResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetInventoryOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/inventory/{inventoryId:guid}/inventory-orders")] HttpRequestData req,
        Guid inventoryId,
        CancellationToken cancellationToken)
    {
        // Resolve flight details so we have the departure date for cabin count lookup.
        var flightDetail = await _offerServiceClient.GetFlightByInventoryIdAsync(inventoryId, cancellationToken);
        if (flightDetail is null)
            return req.CreateResponse(HttpStatusCode.NotFound);

        // Get cabin seat counts for the donut charts.
        var allInventoryResult = await _offerServiceClient.GetFlightInventoryByDateAsync(
            DateOnly.Parse(flightDetail.DepartureDate), cancellationToken: cancellationToken);
        var inventoryGroup = allInventoryResult.Flights.FirstOrDefault(x => x.InventoryId == inventoryId)
            ?? allInventoryResult.PinnedFlights.FirstOrDefault(x => x.InventoryId == inventoryId);

        // Revenue holds represent passengers with a seat held or sold on this flight.
        var holds = await _offerServiceClient.GetInventoryHoldsAsync(inventoryId, cancellationToken);
        var revenueHolds = holds.Where(h => h.HoldType == "Revenue").ToList();

        // Resolve orderIds → booking references, then fetch full order detail in parallel.
        var orderIds = revenueHolds.Select(h => h.OrderId).Distinct().ToList();
        var bookingRefs = await _orderServiceClient.GetBookingReferencesAsync(orderIds, cancellationToken);

        var orderTasks = bookingRefs
            .Where(kv => kv.Value != null)
            .Select(async kv => (kv.Key, await _orderServiceClient.GetOrderByRefAsync(kv.Value!, cancellationToken)));
        var orders = (await Task.WhenAll(orderTasks))
            .Where(r => r.Item2?.OrderData.HasValue == true)
            .ToDictionary(r => r.Key, r => r.Item2!);

        var inventoryIdStr = inventoryId.ToString();
        var rows = BuildInventoryOrderRows(revenueHolds, bookingRefs, orders, inventoryIdStr);

        static CabinCountDto? ToCabinCount(CabinInventoryDto? c) => c is null ? null : new CabinCountDto
        {
            TotalSeats    = c.TotalSeats,
            SeatsSold     = c.SeatsSold,
            SeatsAvailable = c.SeatsAvailable,
            SeatsHeld     = c.SeatsHeld,
        };

        var response = new InventoryOrdersResponse
        {
            InventoryId   = inventoryIdStr,
            FlightNumber  = flightDetail.FlightNumber,
            DepartureDate = flightDetail.DepartureDate,
            DepartureTime = flightDetail.DepartureTime,
            ArrivalTime   = flightDetail.ArrivalTime,
            Origin        = flightDetail.Origin,
            Destination   = flightDetail.Destination,
            AircraftType  = flightDetail.AircraftType,
            Status        = flightDetail.Status,
            Cabins = inventoryGroup is null ? null : new InventoryOrdersCabinsDto
            {
                F = ToCabinCount(inventoryGroup.F),
                J = ToCabinCount(inventoryGroup.J),
                W = ToCabinCount(inventoryGroup.W),
                Y = ToCabinCount(inventoryGroup.Y),
            },
            Orders = rows,
        };

        return await req.OkJsonAsync(response);
    }

    private static IReadOnlyList<InventoryOrderRowDto> BuildInventoryOrderRows(
        IReadOnlyList<FlightInventoryHoldDto> revenueHolds,
        IReadOnlyDictionary<Guid, string?> bookingRefs,
        IDictionary<Guid, OrderMsOrderResult> orders,
        string inventoryId)
    {
        var rows = new List<InventoryOrderRowDto>();

        foreach (var hold in revenueHolds)
        {
            var bookingRef = bookingRefs.GetValueOrDefault(hold.OrderId);
            if (bookingRef is null) continue;

            orders.TryGetValue(hold.OrderId, out var order);
            var orderData = order?.OrderData.HasValue == true
                ? JsonNode.Parse(order.OrderData.Value.GetRawText())?.AsObject()
                : null;

            // Build passengerId (int) → name/type maps from dataLists.passengers.
            var passengerNames = new Dictionary<int, string>();
            var passengerTypes = new Dictionary<int, string>();
            if (orderData?["dataLists"]?["passengers"] is JsonArray paxArray)
            {
                foreach (var p in paxArray)
                {
                    if (p is not JsonObject pObj) continue;
                    var idStr   = pObj["passengerId"]?.GetValue<string>();
                    var given   = pObj["givenName"]?.GetValue<string>() ?? string.Empty;
                    var surname = pObj["surname"]?.GetValue<string>() ?? string.Empty;
                    var type    = pObj["type"]?.GetValue<string>() ?? pObj["ptcCode"]?.GetValue<string>();
                    var id      = ExtractPaxId(idStr);
                    if (id.HasValue)
                    {
                        passengerNames[id.Value] = $"{given} {surname}".Trim();
                        if (type is not null) passengerTypes[id.Value] = type;
                    }
                }
            }

            // Resolve passenger name — prefer hold's stored name if already enriched.
            var passengerName = hold.PassengerName;
            if (string.IsNullOrEmpty(passengerName) && hold.PassengerId.HasValue)
                passengerNames.TryGetValue(hold.PassengerId.Value, out passengerName);

            var passengerType = hold.PassengerId.HasValue
                ? passengerTypes.GetValueOrDefault(hold.PassengerId.Value)
                : null;

            // Find the flight order item for this inventory segment.
            decimal? baseFare = null, tax = null, totalFare = null;
            string? fareFamily = null, fareBasisCode = null;
            int passengerCount = 1;

            if (orderData?["orderItems"] is JsonArray orderItems)
            {
                foreach (var item in orderItems)
                {
                    if (item is not JsonObject oi) continue;
                    var invId = oi["inventoryId"]?.GetValue<string>();
                    if (!string.Equals(invId, inventoryId, StringComparison.OrdinalIgnoreCase)) continue;

                    var pc = oi["passengerCount"]?.GetValue<int>() ?? 1;
                    if (pc < 1) pc = 1;
                    passengerCount = pc;

                    var allFare  = oi["baseFareAmount"]?.GetValue<decimal>();
                    var allTax   = oi["taxAmount"]?.GetValue<decimal>();
                    var allTotal = oi["totalAmount"]?.GetValue<decimal>();

                    baseFare  = allFare.HasValue  ? Math.Round(allFare.Value  / pc, 2, MidpointRounding.AwayFromZero) : null;
                    tax       = allTax.HasValue   ? Math.Round(allTax.Value   / pc, 2, MidpointRounding.AwayFromZero) : null;
                    totalFare = (baseFare.HasValue || tax.HasValue)
                        ? (baseFare ?? 0m) + (tax ?? 0m)
                        : allTotal.HasValue ? Math.Round(allTotal.Value / pc, 2, MidpointRounding.AwayFromZero) : null;

                    fareFamily   = oi["fareFamily"]?.GetValue<string>();
                    fareBasisCode = oi["fareBasisCode"]?.GetValue<string>();
                    break;
                }
            }

            // Build ancillaries for this passenger on this segment.
            var ancillaries = new List<InventoryOrderAncillaryDto>();
            if (orderData?["orderItems"] is JsonArray allOrderItems)
            {
                foreach (var item in allOrderItems)
                {
                    if (item is not JsonObject oi) continue;
                    var pt     = oi["productType"]?.GetValue<string>();
                    var segId  = oi["segmentId"]?.GetValue<string>();
                    var paxId  = ExtractPaxId(oi["passengerId"]?.GetValue<string>());

                    if (!string.Equals(segId, inventoryId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (hold.PassengerId.HasValue && paxId.HasValue && paxId.Value != hold.PassengerId.Value) continue;

                    if (string.Equals(pt, "SEAT", StringComparison.OrdinalIgnoreCase))
                    {
                        var seatNum = oi["seatNumber"]?.GetValue<string>() ?? string.Empty;
                        var price   = oi["price"]?.GetValue<decimal>() ?? 0m;
                        ancillaries.Add(new InventoryOrderAncillaryDto
                        {
                            ProductType = "Seat",
                            Description = $"Seat {seatNum}",
                            Amount      = price,
                        });
                    }
                    else if (string.Equals(pt, "BAG", StringComparison.OrdinalIgnoreCase))
                    {
                        var bags = oi["additionalBags"]?.GetValue<int>() ?? 1;
                        var price = oi["price"]?.GetValue<decimal>() ?? 0m;
                        ancillaries.Add(new InventoryOrderAncillaryDto
                        {
                            ProductType = "Bag",
                            Description = $"+{bags} bag{(bags == 1 ? "" : "s")}",
                            Amount      = price,
                        });
                    }
                    else if (string.Equals(pt, "PRODUCT", StringComparison.OrdinalIgnoreCase))
                    {
                        var name  = oi["name"]?.GetValue<string>() ?? "Product";
                        var price = oi["price"]?.GetValue<decimal>() ?? 0m;
                        ancillaries.Add(new InventoryOrderAncillaryDto
                        {
                            ProductType = "Product",
                            Description = name,
                            Amount      = price,
                        });
                    }
                }
            }

            rows.Add(new InventoryOrderRowDto
            {
                OrderId        = hold.OrderId.ToString(),
                BookingReference = bookingRef,
                Currency       = order?.CurrencyCode ?? "GBP",
                PassengerName  = passengerName,
                PassengerType  = passengerType,
                CabinCode      = hold.CabinCode,
                SeatNumber     = hold.SeatNumber,
                FareFamily     = fareFamily,
                FareBasisCode  = fareBasisCode,
                BaseFareAmount = baseFare,
                TaxAmount      = tax,
                TotalFareAmount = totalFare,
                Ancillaries    = ancillaries,
            });
        }

        return rows;
    }

    /// <summary>
    /// Resolves the passenger name for a single hold row.
    /// Uses passengerId stored on the hold when available.
    /// Falls back to seat-assignment lookup for holds without a stored passengerId.
    /// </summary>
    private static string? ResolvePassengerName(OrderMsOrderResult? order, string inventoryId, string? seatNumber, int? passengerId)
    {
        if (order?.OrderData is not { } data) return null;
        try
        {
            // Build passengerId (int) → full name map from dataLists.passengers.
            var paxById = new Dictionary<int, string>();
            if (data.TryGetProperty("dataLists", out var dataLists) &&
                dataLists.TryGetProperty("passengers", out var passEl) &&
                passEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in passEl.EnumerateArray())
                {
                    var idStr   = p.TryGetProperty("passengerId", out var pid) ? pid.GetString() : null;
                    var given   = p.TryGetProperty("givenName",   out var g)   ? g.GetString()   : null;
                    var surname = p.TryGetProperty("surname",     out var s)   ? s.GetString()   : null;
                    var id      = ExtractPaxId(idStr);
                    if (id.HasValue)
                        paxById[id.Value] = $"{given} {surname}".Trim();
                }
            }

            // Direct lookup via the passengerId stored on the hold (preferred path).
            if (passengerId.HasValue && paxById.TryGetValue(passengerId.Value, out var directName))
                return directName;

            // Seat-assignment lookup for holds created before PassengerId was stored.
            if (!string.IsNullOrEmpty(seatNumber) &&
                data.TryGetProperty("orderItems", out var orderItemsEl) && orderItemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var orderItem in orderItemsEl.EnumerateArray())
                {
                    var pt    = orderItem.TryGetProperty("productType", out var ptEl) ? ptEl.GetString() : null;
                    if (!string.Equals(pt, "SEAT", StringComparison.OrdinalIgnoreCase)) continue;
                    var segId = orderItem.TryGetProperty("segmentId",   out var sid)  ? sid.GetString()  : null;
                    var sn    = orderItem.TryGetProperty("seatNumber",  out var s)    ? s.GetString()    : null;
                    var paxId = ExtractPaxId(orderItem.TryGetProperty("passengerId", out var pid) ? pid.GetString() : null);
                    if (string.Equals(segId, inventoryId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(sn, seatNumber, StringComparison.OrdinalIgnoreCase) &&
                        paxId.HasValue && paxById.TryGetValue(paxId.Value, out var name))
                    {
                        return name;
                    }
                }
            }

            return null;
        }
        catch { return null; }
    }

    private static int? ExtractPaxId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var dash = id.LastIndexOf('-');
        return dash >= 0 && int.TryParse(id[(dash + 1)..], out var n) ? n : int.TryParse(id, out n) ? n : null;
    }
}
