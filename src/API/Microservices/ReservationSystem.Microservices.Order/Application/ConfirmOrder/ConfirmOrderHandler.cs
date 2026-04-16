using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.ConfirmOrder;

/// <summary>
/// Handles the <see cref="ConfirmOrderCommand"/>.
/// Validation is the first operation: order must be Draft, basket must be
/// active and unexpired, and the basket must carry passengers and flight
/// segments. Only after all checks pass does the handler build the full
/// OrderData, assign a booking reference (PNR), transition the order to
/// Confirmed, and delete the basket.
/// </summary>
public sealed class ConfirmOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IBasketRepository _basketRepository;
    private readonly ILogger<ConfirmOrderHandler> _logger;

    public ConfirmOrderHandler(
        IOrderRepository orderRepository,
        IBasketRepository basketRepository,
        ILogger<ConfirmOrderHandler> logger)
    {
        _orderRepository = orderRepository;
        _basketRepository = basketRepository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order> HandleAsync(
        ConfirmOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Confirming order {OrderId}", command.OrderId);

        // ── Step 1: validate everything before doing any work ─────────────────

        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order {command.OrderId} not found.");

        if (order.OrderStatus != OrderStatusValues.Draft)
            throw new InvalidOperationException(
                $"Order cannot be confirmed. Current status: {order.OrderStatus}");

        var basket = await _basketRepository.GetByIdAsync(command.BasketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Basket {command.BasketId} not found.");

        if (!basket.IsActive)
            throw new InvalidOperationException(
                $"Basket is no longer active. Current status: {basket.BasketStatus}");

        if (basket.IsExpired)
            throw new InvalidOperationException("Basket has expired.");

        var basketJson = JsonNode.Parse(basket.BasketData)?.AsObject() ?? new JsonObject();
        var passengersNode = basketJson["passengers"]?.AsArray();
        var segmentsNode = basketJson["flightOffers"]?.AsArray();

        if (passengersNode is null || passengersNode.Count == 0)
            throw new InvalidOperationException("Order cannot be confirmed: no passengers present in basket.");

        if (segmentsNode is null || segmentsNode.Count == 0)
            throw new InvalidOperationException("Order cannot be confirmed: no flight segments present in basket.");

        // Validate that stored seat assignments match the booked cabin for each flight offer
        var seatsNode = basketJson["seats"]?.AsArray();
        if (seatsNode is not null && seatsNode.Count > 0)
        {
            var offerCabinByItemId = segmentsNode
                .OfType<JsonObject>()
                .Where(o => o["basketItemId"]?.GetValue<string>() is not null)
                .ToDictionary(
                    o => o["basketItemId"]!.GetValue<string>(),
                    o => o["cabinCode"]?.GetValue<string>() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var seat in seatsNode)
            {
                var seatCabin = seat?["cabinCode"]?.GetValue<string>();
                if (string.IsNullOrEmpty(seatCabin)) continue;

                var itemRef = seat?["basketItemRef"]?.GetValue<string>();
                if (itemRef is null) continue;

                if (offerCabinByItemId.TryGetValue(itemRef, out var bookedCabin) &&
                    !string.Equals(seatCabin, bookedCabin, StringComparison.OrdinalIgnoreCase))
                {
                    var seatNumber = seat?["seatNumber"]?.GetValue<string>() ?? "unknown";
                    throw new InvalidOperationException(
                        $"Order cannot be confirmed: seat {seatNumber} is in cabin '{seatCabin}' but the booked cabin for basket item '{itemRef}' is '{bookedCabin}'.");
                }
            }
        }

        var ticketingCutoff = DateTime.UtcNow.AddHours(1);
        foreach (var offer in segmentsNode)
        {
            if (offer is not JsonObject offerObj) continue;
            var departureDateTimeStr = offerObj["departureDateTime"]?.GetValue<string>();
            if (departureDateTimeStr is not null &&
                DateTime.TryParse(departureDateTimeStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var departureUtc) &&
                departureUtc <= ticketingCutoff)
            {
                var flightNumber = offerObj["flightNumber"]?.GetValue<string>() ?? "unknown";
                throw new InvalidOperationException(
                    $"Ticketing is closed for flight {flightNumber}: departure at {departureDateTimeStr} is within 1 hour.");
            }
        }

        // ── Step 2: build full OrderData from basket and payment references ───

        // Carry forward bookingType and any reward redemption data from the draft OrderData
        var draftData = JsonNode.Parse(order.OrderData)?.AsObject() ?? new JsonObject();
        var bookingType = draftData["bookingType"]?.GetValue<string>() ?? "Revenue";

        JsonNode? paymentsNode = null;
        try { paymentsNode = JsonNode.Parse(command.PaymentReferencesJson); } catch { }

        // Parse enriched offer data (fare amounts + tax lines from the Offer MS reprice)
        // keyed by offerId+cabinCode so we can overlay onto each flight item.
        var enrichedByKey = ParseEnrichedOffers(command.EnrichedOffersJson);

        // Build flight order items — persist all fare/pricing and flight detail fields so that
        // prices are locked at confirmation time and never re-fetched or re-calculated.
        var flightOrderItems = new JsonArray();
        foreach (var offer in segmentsNode)
        {
            if (offer is not JsonObject offerObj) continue;
            var item = new JsonObject();
            foreach (var prop in new[]
            {
                "offerId", "inventoryId", "cabinCode",
                "flightNumber", "departureDate", "departureTime", "arrivalTime",
                "origin", "destination", "aircraftType",
                "fareBasisCode", "fareFamily",
                "totalAmount", "baseFareAmount", "taxAmount",
                "isRefundable", "isChangeable",
                "pointsPrice", "pointsTaxes"
            })
            {
                if (offerObj[prop] is JsonNode val && val.GetValueKind() != System.Text.Json.JsonValueKind.Null)
                    item[prop] = val.DeepClone();
            }

            // Overlay the repriced fare amounts and tax lines if available.
            var offerId    = offerObj["offerId"]?.GetValue<string>() ?? "";
            var cabinCode  = offerObj["cabinCode"]?.GetValue<string>() ?? "";
            var lookupKey  = $"{offerId}:{cabinCode}";
            if (enrichedByKey.TryGetValue(lookupKey, out var enriched))
            {
                item["baseFareAmount"] = enriched.BaseFareAmount;
                item["taxAmount"]      = enriched.TaxAmount;
                item["totalAmount"]    = enriched.TotalAmount;

                if (enriched.TaxLines is { Count: > 0 })
                {
                    var taxLinesArray = new JsonArray();
                    foreach (var tl in enriched.TaxLines)
                    {
                        var tlNode = new JsonObject
                        {
                            ["code"]   = tl.Code,
                            ["amount"] = tl.Amount
                        };
                        if (tl.Description is not null)
                            tlNode["description"] = tl.Description;
                        taxLinesArray.Add(tlNode);
                    }
                    item["taxLines"] = taxLinesArray;
                }
            }

            flightOrderItems.Add(item);
        }

        var orderData = new JsonObject
        {
            ["currency"] = basket.CurrencyCode,
            ["dataLists"] = new JsonObject
            {
                ["passengers"] = passengersNode.DeepClone()
            },
            ["orderItems"] = flightOrderItems,
            ["payments"] = paymentsNode?.DeepClone() ?? new JsonArray(),
            ["bookingType"] = bookingType,
            ["history"] = new JsonArray
            {
                draftData["history"]?[0]?.DeepClone() ?? new JsonObject
                {
                    ["event"] = "OrderCreated",
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                },
                new JsonObject
                {
                    ["event"] = "OrderConfirmed",
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                }
            }
        };

        // Only include optional arrays when non-empty; downstream handlers create the key when needed.
        if (basketJson["seats"]?.AsArray() is { Count: > 0 } seats)
        {
            // Strip basket-lifecycle fields (basketItemRef, cabinCode) that have no meaning
            // after confirmation; cabinCode is already present on the matching order item.
            var seatAssignments = new JsonArray();
            foreach (var seat in seats)
            {
                if (seat is not JsonObject seatObj) continue;
                var stripped = new JsonObject();
                foreach (var prop in seatObj)
                {
                    if (prop.Key is "basketItemRef" or "cabinCode") continue;
                    stripped[prop.Key] = prop.Value?.DeepClone();
                }
                seatAssignments.Add(stripped);
            }
            orderData["seatAssignments"] = seatAssignments;
        }
        if (basketJson["bags"]?.AsArray() is { Count: > 0 } bags)
            orderData["bagItems"] = bags.DeepClone();
        if (basketJson["ssrSelections"]?.AsArray() is { Count: > 0 } ssrs)
            orderData["ssrItems"] = ssrs.DeepClone();
        if (basketJson["products"]?.AsArray() is { Count: > 0 } products)
            orderData["productItems"] = products.DeepClone();

        if (draftData["pointsRedemption"] is JsonNode redemption)
            orderData["pointsRedemption"] = redemption.DeepClone();

        // ── Step 3: confirm order, persist, delete basket ─────────────────────

        // Generate a booking reference that is unique in the database, retrying on the rare
        // chance of a collision (keyspace is 36^6 ≈ 2.2 billion, but collisions are possible).
        const int maxPnrAttempts = 5;
        string bookingReference = string.Empty;
        for (var attempt = 1; attempt <= maxPnrAttempts; attempt++)
        {
            var candidate = GenerateBookingReference();
            var existing = await _orderRepository.GetByBookingReferenceAsync(candidate, cancellationToken);
            if (existing is null)
            {
                bookingReference = candidate;
                break;
            }
            _logger.LogWarning(
                "Booking reference collision on attempt {Attempt}/{Max}, regenerating", attempt, maxPnrAttempts);
        }

        if (string.IsNullOrEmpty(bookingReference))
            throw new InvalidOperationException(
                $"Unable to generate a unique booking reference after {maxPnrAttempts} attempts.");

        order.Confirm(
            bookingReference,
            basket.TotalAmount ?? order.TotalAmount ?? 0m,
            orderData.ToJsonString(),
            basket.ExpiresAt);

        await _orderRepository.UpdateAsync(order, cancellationToken);

        basket.Confirm(order.OrderId);
        await _basketRepository.UpdateAsync(basket, cancellationToken);
        await _basketRepository.DeleteAsync(basket.BasketId, cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} confirmed with booking reference {BookingReference}",
            order.OrderId, bookingReference);

        return order;
    }

    private static string GenerateBookingReference()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return RandomNumberGenerator.GetString(chars, 6);
    }

    /// <summary>
    /// Parses the enriched offers JSON (produced by the Retail API from the Offer MS reprice)
    /// into a lookup keyed by "offerId:cabinCode".
    /// </summary>
    private static Dictionary<string, EnrichedOfferEntry> ParseEnrichedOffers(string? enrichedOffersJson)
    {
        var map = new Dictionary<string, EnrichedOfferEntry>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(enrichedOffersJson)) return map;

        try
        {
            using var doc = JsonDocument.Parse(enrichedOffersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return map;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var offerIdStr = item.TryGetProperty("offerId",   out var oi) ? oi.GetString() ?? "" : "";
                var cabinCode  = item.TryGetProperty("cabinCode", out var cc) ? cc.GetString() ?? "" : "";
                var key        = $"{offerIdStr}:{cabinCode}";

                List<TaxLineEntry>? taxLines = null;
                if (item.TryGetProperty("taxLines", out var tlEl) && tlEl.ValueKind == JsonValueKind.Array)
                {
                    taxLines = new List<TaxLineEntry>();
                    foreach (var tl in tlEl.EnumerateArray())
                    {
                        taxLines.Add(new TaxLineEntry
                        {
                            Code        = tl.TryGetProperty("code",        out var c) ? c.GetString() ?? "" : "",
                            Amount      = tl.TryGetProperty("amount",      out var a) ? a.GetDecimal()      : 0m,
                            Description = tl.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null
                        });
                    }
                }

                map[key] = new EnrichedOfferEntry
                {
                    BaseFareAmount = item.TryGetProperty("baseFareAmount", out var bf) ? bf.GetDecimal() : 0m,
                    TaxAmount      = item.TryGetProperty("taxAmount",      out var ta) ? ta.GetDecimal() : 0m,
                    TotalAmount    = item.TryGetProperty("totalAmount",    out var tot) ? tot.GetDecimal() : 0m,
                    TaxLines       = taxLines
                };
            }
        }
        catch { /* Return whatever was parsed */ }

        return map;
    }

    private sealed class EnrichedOfferEntry
    {
        public decimal BaseFareAmount { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal TotalAmount { get; init; }
        public List<TaxLineEntry>? TaxLines { get; init; }
    }

    private sealed class TaxLineEntry
    {
        public string Code { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string? Description { get; init; }
    }
}
