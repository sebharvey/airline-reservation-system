using System.Text.Json;
using System.Text.Json.Nodes;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetAdminOrderDetail;

public sealed class GetAdminOrderDetailHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;

    public GetAdminOrderDetailHandler(
        OrderServiceClient orderServiceClient,
        OfferServiceClient offerServiceClient,
        PaymentServiceClient paymentServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
        _paymentServiceClient = paymentServiceClient;
    }

    public async Task<AdminOrderDetailResponse?> HandleAsync(string bookingReference, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, cancellationToken);
        if (order is null)
            return null;

        var enrichedOrderData = order.OrderData.HasValue
            ? await EnrichOrderDataAsync(order.OrderData.Value, order.CurrencyCode, cancellationToken)
            : order.OrderData;

        return new AdminOrderDetailResponse
        {
            OrderId = order.OrderId,
            BookingReference = order.BookingReference ?? bookingReference,
            OrderStatus = order.OrderStatus,
            ChannelCode = order.ChannelCode,
            Currency = order.CurrencyCode,
            TotalAmount = order.TotalAmount,
            TicketingTimeLimit = order.TicketingTimeLimit,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Version = order.Version,
            OrderData = enrichedOrderData,
        };
    }

    private async Task<JsonElement?> EnrichOrderDataAsync(
        JsonElement orderDataElement,
        string currencyCode,
        CancellationToken ct)
    {
        try
        {
            var orderData = JsonNode.Parse(orderDataElement.GetRawText())?.AsObject();
            if (orderData is null) return orderDataElement;

            await EnrichFlightSegmentsAsync(orderData, ct);
            await EnrichPaymentsAsync(orderData, currencyCode, ct);
            EnrichHistory(orderData);

            using var doc = JsonDocument.Parse(orderData.ToJsonString());
            return doc.RootElement.Clone();
        }
        catch
        {
            return orderDataElement;
        }
    }

    /// <summary>
    /// Resolves flight details for each orderItem by calling the Offer MS with the stored
    /// inventoryId, then populates dataLists.flightSegments and rebuilds orderItems with
    /// per-passenger e-ticket data so the Terminal itinerary tab renders correctly.
    /// </summary>
    private async Task EnrichFlightSegmentsAsync(JsonObject orderData, CancellationToken ct)
    {
        var orderItemsNode = orderData["orderItems"]?.AsArray();
        if (orderItemsNode is null || orderItemsNode.Count == 0)
            return;

        var eTicketsNode = orderData["eTickets"]?.AsArray();

        var bookingType = orderData["bookingType"]?.GetValue<string>() ?? "Revenue";
        var itemStatus  = bookingType == "Standby" ? "Standby" : "Confirmed";

        var flightSegments = new JsonArray();
        var enrichedItems = new JsonArray();

        // Build segment description lookup (inventoryId → "AX001 LHR→JFK") for use by seat items below.
        var segmentDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var n = 0; n < orderItemsNode.Count; n++)
        {
            var item = orderItemsNode[n]?.AsObject();
            if (item is null) continue;

            if (!item.TryGetPropertyValue("inventoryId", out var invNode) || invNode is null)
                continue;

            var inventoryIdStr = invNode.GetValue<string>();
            if (!Guid.TryParse(inventoryIdStr, out var inventoryId))
                continue;

            // basketItemId stored on the orderItem (new orders) or derived from position (legacy)
            var basketItemId = item["basketItemId"]?.GetValue<string>() ?? $"BI-{n + 1}";
            var cabinCode = item["cabinCode"]?.GetValue<string>() ?? string.Empty;

            var flightDetail = await _offerServiceClient.GetFlightByInventoryIdAsync(inventoryId, ct);
            if (flightDetail is null) continue;

            // Construct ISO 8601 UTC datetimes from the date + time strings stored in the inventory
            var depDate = flightDetail.DepartureDate; // yyyy-MM-dd
            var arrDate = flightDetail.ArrivalDayOffset > 0
                ? DateOnly.Parse(depDate).AddDays(flightDetail.ArrivalDayOffset).ToString("yyyy-MM-dd")
                : depDate;

            var departureIso = $"{depDate}T{flightDetail.DepartureTime}:00Z";
            var arrivalIso = $"{arrDate}T{flightDetail.ArrivalTime}:00Z";

            flightSegments.Add(new JsonObject
            {
                ["segmentId"] = inventoryIdStr,
                ["flightNumber"] = flightDetail.FlightNumber,
                ["origin"] = flightDetail.Origin,
                ["destination"] = flightDetail.Destination,
                ["departureTime"] = departureIso,
                ["arrivalTime"] = arrivalIso,
                ["cabinClass"] = cabinCode,
                ["fareClass"] = (JsonNode?)null,
                ["departureDate"] = depDate,
            });

            var segDesc = $"{flightDetail.FlightNumber} {flightDetail.Origin}→{flightDetail.Destination}";
            segmentDescriptions[inventoryIdStr] = segDesc;

            // Build one enriched Flight orderItem per passenger eTicket for this segment.
            // If no eTickets exist yet (pre-ticketing), emit a single item with no passenger/eTicket
            // so the flight always appears in the Order Items tab.
            var matchingETickets = new List<(string paxId, string eTicketNumber)>();
            if (eTicketsNode is not null)
            {
                foreach (var eTicketNode in eTicketsNode)
                {
                    var et = eTicketNode?.AsObject();
                    if (et is null) continue;

                    var etSegId = et["SegmentId"]?.GetValue<string>()
                               ?? et["segmentId"]?.GetValue<string>()
                               ?? string.Empty;

                    if (etSegId != basketItemId) continue;

                    var paxId = et["PassengerId"]?.GetValue<string>()
                             ?? et["passengerId"]?.GetValue<string>()
                             ?? string.Empty;

                    var eTicketNumber = et["ETicketNumber"]?.GetValue<string>()
                                     ?? et["eTicketNumber"]?.GetValue<string>()
                                     ?? string.Empty;

                    matchingETickets.Add((paxId, eTicketNumber));
                }
            }

            var allPaxFare  = item["baseFareAmount"] is JsonNode fareNode ? fareNode.GetValue<decimal>() : (decimal?)null;
            var allPaxTax   = item["taxAmount"]      is JsonNode taxNode  ? taxNode.GetValue<decimal>()  : (decimal?)null;
            var allPaxTotal = item["totalAmount"]    is JsonNode totNode  ? totNode.GetValue<decimal>()  : (decimal?)null;
            var taxLinesNode = item["taxLines"]      is JsonNode tlNode   ? tlNode.DeepClone()           : null;
            var currency     = orderData["currency"] is JsonNode curNode  ? curNode.DeepClone()          : null;
            var passengerCount = item["passengerCount"] is JsonNode pcNode ? pcNode.GetValue<int>() : 1;
            if (passengerCount < 1) passengerCount = 1;

            // Pre-ticketing: one item with all-pax amounts and "(N pax)" description.
            // Ticketed: one item per passenger eTicket showing each passenger's per-pax share
            // (so the subtotal row sums correctly without double-counting).
            var itemDesc = passengerCount > 1 ? $"{segDesc} ({passengerCount} pax)" : segDesc;

            if (matchingETickets.Count > 0)
            {
                var perPaxFare  = allPaxFare.HasValue  ? Math.Round(allPaxFare.Value  / passengerCount, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
                var perPaxTax   = allPaxTax.HasValue   ? Math.Round(allPaxTax.Value   / passengerCount, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
                // Derive lineTotal from the already-rounded fare + tax so per-row and footer totals are consistent.
                var perPaxTotal = perPaxFare.HasValue || perPaxTax.HasValue
                    ? (decimal?)((perPaxFare ?? 0m) + (perPaxTax ?? 0m))
                    : (allPaxTotal.HasValue ? Math.Round(allPaxTotal.Value / passengerCount, 2, MidpointRounding.AwayFromZero) : (decimal?)null);

                // Divide tax lines proportionally so the Fare modal shows per-pax line items.
                JsonNode? perPaxTaxLines = null;
                if (taxLinesNode is JsonArray tlArray && passengerCount > 1)
                {
                    var divided = new JsonArray();
                    foreach (var tl in tlArray)
                    {
                        if (tl is not JsonObject tlObj) continue;
                        var tlAmt = tlObj["amount"] is JsonNode a ? a.GetValue<decimal>() : 0m;
                        divided.Add(new JsonObject
                        {
                            ["code"]        = tlObj["code"]?.DeepClone(),
                            ["amount"]      = Math.Round(tlAmt / passengerCount, 2, MidpointRounding.AwayFromZero),
                            ["description"] = tlObj["description"]?.DeepClone(),
                        });
                    }
                    perPaxTaxLines = divided;
                }
                else
                {
                    perPaxTaxLines = taxLinesNode?.DeepClone();
                }

                foreach (var (paxId, eTicketNumber) in matchingETickets)
                {
                    enrichedItems.Add(new JsonObject
                    {
                        ["itemId"]         = Guid.NewGuid().ToString(),
                        ["itemType"]       = "Flight",
                        ["description"]    = segDesc,
                        ["passengerId"]    = paxId,
                        ["segmentId"]      = inventoryIdStr,
                        ["status"]         = itemStatus,
                        ["eTicketNumber"]  = !string.IsNullOrEmpty(eTicketNumber) ? (JsonNode)eTicketNumber : null,
                        ["seatNumber"]     = null,
                        ["bagWeightKg"]    = null,
                        ["fareAmount"]     = perPaxFare.HasValue  ? (JsonNode)perPaxFare.Value  : null,
                        ["taxAmount"]      = perPaxTax.HasValue   ? (JsonNode)perPaxTax.Value   : null,
                        ["totalAmount"]    = perPaxTotal.HasValue ? (JsonNode)perPaxTotal.Value : null,
                        ["lineTotal"]      = perPaxTotal.HasValue ? (JsonNode)perPaxTotal.Value : null,
                        ["taxLines"]       = perPaxTaxLines?.DeepClone(),
                        ["amount"]         = null,
                        ["currency"]       = currency?.DeepClone(),
                        ["passengerCount"] = 1,
                    });
                }
            }
            else
            {
                enrichedItems.Add(new JsonObject
                {
                    ["itemId"]         = Guid.NewGuid().ToString(),
                    ["itemType"]       = "Flight",
                    ["description"]    = itemDesc,
                    ["passengerId"]    = null,
                    ["segmentId"]      = inventoryIdStr,
                    ["status"]         = itemStatus,
                    ["eTicketNumber"]  = null,
                    ["seatNumber"]     = null,
                    ["bagWeightKg"]    = null,
                    ["fareAmount"]     = allPaxFare.HasValue  ? (JsonNode)allPaxFare.Value  : null,
                    ["taxAmount"]      = allPaxTax.HasValue   ? (JsonNode)allPaxTax.Value   : null,
                    ["totalAmount"]    = allPaxTotal.HasValue ? (JsonNode)allPaxTotal.Value : null,
                    ["lineTotal"]      = allPaxTotal.HasValue ? (JsonNode)allPaxTotal.Value : null,
                    ["taxLines"]       = taxLinesNode?.DeepClone(),
                    ["amount"]         = null,
                    ["currency"]       = currency?.DeepClone(),
                    ["passengerCount"] = passengerCount,
                });
            }
        }

        // Populate dataLists.flightSegments
        if (orderData["dataLists"] is JsonObject dataLists)
            dataLists["flightSegments"] = flightSegments;
        else
            orderData["dataLists"] = new JsonObject { ["flightSegments"] = flightSegments };

        // Append seat ancillary items from orderItems (productType=SEAT) so the Terminal
        // passengers tab can display seat numbers via getSeatForPaxSegment (itemType=Seat).
        for (var s = 0; s < orderItemsNode.Count; s++)
        {
            var seatItem = orderItemsNode[s]?.AsObject();
            if (seatItem is null) continue;
            var seatPt = seatItem["productType"]?.GetValue<string>();
            if (!string.Equals(seatPt, "SEAT", StringComparison.OrdinalIgnoreCase)) continue;
            var seatNum = seatItem["seatNumber"]?.GetValue<string>() ?? string.Empty;
            var seatSegId = seatItem["segmentId"]?.GetValue<string>() ?? string.Empty;
            var seatFlightDesc = segmentDescriptions.TryGetValue(seatSegId, out var sd) ? $" — {sd}" : string.Empty;
            var seatPrice = seatItem["price"] is JsonNode seatPriceNode ? seatPriceNode.GetValue<decimal>() : 0m;
            var seatTax   = seatItem["tax"]   is JsonNode seatTaxNode   ? seatTaxNode.GetValue<decimal>()   : 0m;
            enrichedItems.Add(new JsonObject
            {
                ["itemId"]        = Guid.NewGuid().ToString(),
                ["itemType"]      = "Seat",
                ["description"]   = $"Seat {seatNum}{seatFlightDesc}",
                ["passengerId"]   = seatItem["passengerId"]?.GetValue<string>(),
                ["segmentId"]     = seatSegId,
                ["status"]        = "Confirmed",
                ["eTicketNumber"] = null,
                ["seatNumber"]    = seatNum,
                ["bagWeightKg"]   = null,
                ["name"]          = null,
                ["amount"]        = seatPrice,
                ["taxAmount"]     = seatTax,
                ["lineTotal"]     = Math.Round(seatPrice + seatTax, 2),
                ["currency"]      = seatItem["currency"]?.GetValue<string>(),
            });
        }

        // Pass through SERVICE (SSR) and PRODUCT items from raw orderItems,
        // adding a human-readable description for display in the Order Items tab.
        for (var p = 0; p < orderItemsNode.Count; p++)
        {
            var rawItem = orderItemsNode[p]?.AsObject();
            if (rawItem is null) continue;
            var pt = rawItem["productType"]?.GetValue<string>();

            if (string.Equals(pt, "SERVICE", StringComparison.OrdinalIgnoreCase))
            {
                var ssrCode = rawItem["ssrCode"]?.GetValue<string>() ?? string.Empty;
                var paxRef  = rawItem["passengerRef"]?.GetValue<string>() ?? string.Empty;
                var segRef  = rawItem["segmentRef"]?.GetValue<string>() ?? string.Empty;
                var segDesc = segmentDescriptions.TryGetValue(segRef, out var sd) ? $" — {sd}" : string.Empty;
                enrichedItems.Add(new JsonObject
                {
                    ["itemId"]        = Guid.NewGuid().ToString(),
                    ["itemType"]      = "SSR",
                    ["description"]   = $"{ssrCode}{segDesc}",
                    ["passengerId"]   = paxRef,
                    ["segmentId"]     = segRef,
                    ["status"]        = "Confirmed",
                    ["ssrCode"]       = ssrCode,
                    ["eTicketNumber"] = null,
                    ["seatNumber"]    = null,
                    ["bagWeightKg"]   = null,
                    ["amount"]        = null,
                    ["lineTotal"]     = null,
                    ["currency"]      = null,
                });
            }
            else if (string.Equals(pt, "PRODUCT", StringComparison.OrdinalIgnoreCase))
            {
                var productName = rawItem["name"]?.GetValue<string>() ?? "Product";
                var segRef      = rawItem["segmentRef"]?.GetValue<string>();
                var segDesc     = segRef is not null && segmentDescriptions.TryGetValue(segRef, out var sd2)
                                  ? $" — {sd2}" : string.Empty;
                var productPrice = rawItem["price"] is JsonNode priceNode ? priceNode.GetValue<decimal>() : 0m;
                var productTax   = rawItem["tax"]   is JsonNode taxNode2   ? taxNode2.GetValue<decimal>()   : 0m;
                enrichedItems.Add(new JsonObject
                {
                    ["itemId"]        = rawItem["basketItemId"]?.GetValue<string>() ?? Guid.NewGuid().ToString(),
                    ["itemType"]      = "Product",
                    ["description"]   = $"{productName}{segDesc}",
                    ["passengerId"]   = rawItem["passengerId"]?.GetValue<string>(),
                    ["segmentId"]     = segRef,
                    ["status"]        = "Confirmed",
                    ["eTicketNumber"] = null,
                    ["seatNumber"]    = null,
                    ["bagWeightKg"]   = null,
                    ["name"]          = productName,
                    ["amount"]        = productPrice,
                    ["taxAmount"]     = productTax,
                    ["lineTotal"]     = Math.Round(productPrice + productTax, 2),
                    ["currency"]      = rawItem["currency"]?.GetValue<string>(),
                });
            }
            else if (string.Equals(pt, "BAG", StringComparison.OrdinalIgnoreCase))
            {
                var addBags  = rawItem["additionalBags"] is JsonNode abNode ? abNode.GetValue<int>() : 1;
                var paxId    = rawItem["passengerId"]?.GetValue<string>();
                var segRef   = rawItem["segmentId"]?.GetValue<string>();
                var segDesc  = segRef is not null && segmentDescriptions.TryGetValue(segRef, out var bsd)
                               ? $" — {bsd}" : string.Empty;
                var bagPrice = rawItem["price"] is JsonNode bagPriceNode ? bagPriceNode.GetValue<decimal>() : 0m;
                var bagTax   = rawItem["tax"]   is JsonNode bagTaxNode   ? bagTaxNode.GetValue<decimal>()   : 0m;
                enrichedItems.Add(new JsonObject
                {
                    ["itemId"]         = Guid.NewGuid().ToString(),
                    ["itemType"]       = "Bag",
                    ["description"]    = $"+{addBags} bag{(addBags == 1 ? "" : "s")}{segDesc}",
                    ["passengerId"]    = paxId,
                    ["segmentId"]      = segRef,
                    ["status"]         = "Confirmed",
                    ["eTicketNumber"]  = null,
                    ["seatNumber"]     = null,
                    ["bagWeightKg"]    = null,
                    ["additionalBags"] = addBags,
                    ["amount"]         = bagPrice,
                    ["taxAmount"]      = bagTax,
                    ["lineTotal"]      = Math.Round(bagPrice + bagTax, 2),
                    ["currency"]       = rawItem["currency"]?.GetValue<string>(),
                });
            }
        }

        if (enrichedItems.Count > 0)
            orderData["orderItems"] = enrichedItems;

        // Compute order item subtotals server-side so the Terminal never calculates totals itself.
        decimal subtotalFare = 0m;
        decimal subtotalTax  = 0m;
        foreach (var node in enrichedItems)
        {
            if (node is not JsonObject ei) continue;
            subtotalFare += ei["fareAmount"]?.GetValue<decimal>() ?? ei["amount"]?.GetValue<decimal>() ?? 0m;
            subtotalTax  += ei["taxAmount"]?.GetValue<decimal>() ?? 0m;
        }
        // Derive grandTotal from subtotals so all three figures are always internally consistent.
        var grandTotal = Math.Round(subtotalFare + subtotalTax, 2);

        var orderCurrency = orderData["currency"]?.GetValue<string>() ?? "GBP";
        orderData["itemTotals"] = new JsonObject
        {
            ["subtotalFare"] = Math.Round(subtotalFare, 2),
            ["subtotalTax"]  = Math.Round(subtotalTax,  2),
            ["grandTotal"]   = Math.Round(grandTotal,   2),
            ["currency"]     = orderCurrency,
        };
    }

    /// <summary>
    /// Replaces the bare payment references stored in orderData.payments with full payment
    /// records (status, method, timestamps, events) fetched from the Payment MS.
    /// </summary>
    private async Task EnrichPaymentsAsync(JsonObject orderData, string currencyCode, CancellationToken ct)
    {
        var paymentsNode = orderData["payments"]?.AsArray();
        if (paymentsNode is null || paymentsNode.Count == 0)
            return;

        var enrichedPayments = new JsonArray();

        foreach (var paymentRef in paymentsNode)
        {
            var payRef = paymentRef?.AsObject();
            if (payRef is null) continue;

            var paymentId = payRef["paymentReference"]?.GetValue<string>() ?? string.Empty;

            if (Guid.TryParse(paymentId, out _))
            {
                var detail = await _paymentServiceClient.GetPaymentAsync(paymentId, ct);
                if (detail is not null)
                {
                    var eventsNode = new JsonArray();
                    var events = await _paymentServiceClient.GetPaymentEventsAsync(paymentId, ct);
                    foreach (var ev in events)
                    {
                        eventsNode.Add(new JsonObject
                        {
                            ["eventType"] = ev.EventType,
                            ["productType"] = ev.ProductType,
                            ["amount"] = ev.Amount,
                            ["currency"] = ev.CurrencyCode,
                            ["notes"] = ev.Notes,
                            ["createdAt"] = ev.CreatedAt.ToString("o"),
                        });
                    }

                    enrichedPayments.Add(new JsonObject
                    {
                        ["paymentId"] = paymentId,
                        ["amount"] = detail.Amount,
                        ["currency"] = detail.CurrencyCode,
                        ["status"] = detail.Status,
                        ["paymentMethod"] = detail.Method,
                        ["cardType"] = detail.CardType,
                        ["cardLast4"] = detail.CardLast4,
                        ["authorisedAt"] = detail.AuthorisedAt?.ToString("o"),
                        ["settledAt"] = detail.SettledAt?.ToString("o"),
                        ["events"] = eventsNode,
                    });
                    continue;
                }
            }

            // Fallback when payment record cannot be found in the Payment MS
            enrichedPayments.Add(new JsonObject
            {
                ["paymentId"] = paymentId,
                ["amount"] = 0m,
                ["currency"] = currencyCode,
                ["status"] = "Unknown",
                ["paymentMethod"] = null,
                ["authorisedAt"] = null,
                ["settledAt"] = null,
                ["events"] = new JsonArray(),
            });
        }

        orderData["payments"] = enrichedPayments;
    }

    /// <summary>
    /// Maps history entries from the internal format (event/timestamp) to the format
    /// expected by the Terminal app (eventType/description/timestamp).
    /// </summary>
    private static void EnrichHistory(JsonObject orderData)
    {
        var historyNode = orderData["history"]?.AsArray();
        if (historyNode is null || historyNode.Count == 0)
            return;

        var enrichedHistory = new JsonArray();
        foreach (var entry in historyNode)
        {
            var h = entry?.AsObject();
            if (h is null) continue;

            var eventType = h["eventType"]?.GetValue<string>()
                         ?? h["event"]?.GetValue<string>()
                         ?? string.Empty;

            enrichedHistory.Add(new JsonObject
            {
                ["eventType"] = eventType,
                ["description"] = h["description"]?.GetValue<string>() ?? string.Empty,
                ["timestamp"] = h["timestamp"]?.GetValue<string>() ?? string.Empty,
            });
        }

        orderData["history"] = enrichedHistory;
    }
}
