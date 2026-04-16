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

            // Build one enriched orderItem per passenger for this segment, carrying the e-ticket
            // number so the Terminal can look it up by passengerId + segmentId.
            if (eTicketsNode is not null)
            {
                foreach (var eTicketNode in eTicketsNode)
                {
                    var et = eTicketNode?.AsObject();
                    if (et is null) continue;

                    // eTickets store SegmentId as the original basketItemId (e.g. "BI-1")
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

                    enrichedItems.Add(new JsonObject
                    {
                        ["itemId"] = Guid.NewGuid().ToString(),
                        ["itemType"] = "Flight",
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
        }

        // Populate dataLists.flightSegments
        if (orderData["dataLists"] is JsonObject dataLists)
            dataLists["flightSegments"] = flightSegments;
        else
            orderData["dataLists"] = new JsonObject { ["flightSegments"] = flightSegments };

        // Append seat ancillary items from seatAssignments so the Terminal passengers tab
        // can display seat numbers via getSeatForPaxSegment (which looks for itemType=Seat).
        // Seats are stored with segmentId = inventoryId (set by the web app at selection time).
        var seatAssignmentsNode = orderData["seatAssignments"]?.AsArray();
        if (seatAssignmentsNode is not null)
        {
            foreach (var seatNode in seatAssignmentsNode)
            {
                var seat = seatNode?.AsObject();
                if (seat is null) continue;
                enrichedItems.Add(new JsonObject
                {
                    ["itemId"] = Guid.NewGuid().ToString(),
                    ["itemType"] = "Seat",
                    ["passengerId"] = seat["passengerId"]?.GetValue<string>(),
                    ["segmentId"] = seat["segmentId"]?.GetValue<string>(),
                    ["status"] = "Confirmed",
                    ["eTicketNumber"] = null,
                    ["seatNumber"] = seat["seatNumber"]?.GetValue<string>(),
                    ["bagWeightKg"] = null,
                    ["name"] = null,
                    ["amount"] = seat["price"] is JsonNode priceNode ? priceNode.DeepClone() : null,
                    ["currency"] = seat["currency"]?.GetValue<string>(),
                });
            }
        }

        // Append product ancillary items from productItems so the Terminal ancillaries tab
        // can display purchased products (e.g. lounge access, priority boarding).
        var productItemsNode = orderData["productItems"]?.AsArray();
        if (productItemsNode is not null)
        {
            foreach (var productNode in productItemsNode)
            {
                var product = productNode?.AsObject();
                if (product is null) continue;
                enrichedItems.Add(new JsonObject
                {
                    ["itemId"] = product["basketItemId"]?.GetValue<string>() ?? Guid.NewGuid().ToString(),
                    ["itemType"] = "Product",
                    ["passengerId"] = product["passengerId"]?.GetValue<string>(),
                    ["segmentId"] = product["segmentRef"]?.GetValue<string>(),
                    ["status"] = "Confirmed",
                    ["eTicketNumber"] = null,
                    ["seatNumber"] = null,
                    ["bagWeightKg"] = null,
                    ["name"] = product["name"]?.GetValue<string>(),
                    ["amount"] = product["price"] is JsonNode priceNode ? priceNode.DeepClone() : null,
                    ["currency"] = product["currency"]?.GetValue<string>(),
                });
            }
        }

        // Replace orderItems only when we have enriched entries (prevents wiping ancillary items
        // that may have been appended by future post-sale operations)
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
