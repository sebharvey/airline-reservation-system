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

            if (matchingETickets.Count > 0)
            {
                foreach (var (paxId, eTicketNumber) in matchingETickets)
                {
                    enrichedItems.Add(new JsonObject
                    {
                        ["itemId"] = Guid.NewGuid().ToString(),
                        ["itemType"] = "Flight",
                        ["description"] = segDesc,
                        ["passengerId"] = paxId,
                        ["segmentId"] = inventoryIdStr,
                        ["status"] = "Confirmed",
                        ["eTicketNumber"] = !string.IsNullOrEmpty(eTicketNumber) ? (JsonNode)eTicketNumber : null,
                        ["seatNumber"] = null,
                        ["bagWeightKg"] = null,
                        ["amount"] = null,
                        ["currency"] = null,
                    });
                }
            }
            else
            {
                enrichedItems.Add(new JsonObject
                {
                    ["itemId"] = Guid.NewGuid().ToString(),
                    ["itemType"] = "Flight",
                    ["description"] = segDesc,
                    ["passengerId"] = null,
                    ["segmentId"] = inventoryIdStr,
                    ["status"] = "Confirmed",
                    ["eTicketNumber"] = null,
                    ["seatNumber"] = null,
                    ["bagWeightKg"] = null,
                    ["amount"] = null,
                    ["currency"] = null,
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
                ["amount"]        = seatItem["price"] is JsonNode priceNode ? priceNode.DeepClone() : null,
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
                    ["currency"]      = null,
                });
            }
            else if (string.Equals(pt, "PRODUCT", StringComparison.OrdinalIgnoreCase))
            {
                var productName = rawItem["name"]?.GetValue<string>() ?? "Product";
                var segRef      = rawItem["segmentRef"]?.GetValue<string>();
                var segDesc     = segRef is not null && segmentDescriptions.TryGetValue(segRef, out var sd2)
                                  ? $" — {sd2}" : string.Empty;
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
                    ["amount"]        = rawItem["price"] is JsonNode priceNode ? priceNode.DeepClone() : null,
                    ["currency"]      = rawItem["currency"]?.GetValue<string>(),
                });
            }
        }

        if (enrichedItems.Count > 0)
            orderData["orderItems"] = enrichedItems;
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
            var fallbackAmount = payRef["amount"] is JsonNode amtNode ? amtNode.GetValue<decimal>() : 0m;
            var fallbackMethod = payRef["type"]?.GetValue<string>();

            JsonObject enrichedPayment;

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
                            ["amount"] = ev.Amount,
                            ["currency"] = ev.CurrencyCode,
                            ["notes"] = ev.Notes,
                            ["createdAt"] = ev.CreatedAt.ToString("o"),
                        });
                    }

                    enrichedPayment = new JsonObject
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
                    };

                    enrichedPayments.Add(enrichedPayment);
                    continue;
                }
            }

            // Fallback when payment record cannot be found
            enrichedPayments.Add(new JsonObject
            {
                ["paymentId"] = paymentId,
                ["amount"] = fallbackAmount,
                ["currency"] = currencyCode,
                ["status"] = "Unknown",
                ["paymentMethod"] = fallbackMethod,
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
