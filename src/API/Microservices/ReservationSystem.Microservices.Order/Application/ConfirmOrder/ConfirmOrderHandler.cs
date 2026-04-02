using System.Security.Cryptography;
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

        // Build flight order items — persist all fare/pricing and flight detail fields so that
        // prices are locked at confirmation time and never re-fetched or re-calculated.
        var flightOrderItems = new JsonArray();
        foreach (var offer in segmentsNode)
        {
            if (offer is not JsonObject offerObj) continue;
            var item = new JsonObject();
            foreach (var prop in new[]
            {
                "offerId", "sessionId", "inventoryId", "cabinCode", "basketItemId",
                "flightNumber", "departureDate", "departureTime", "arrivalTime",
                "origin", "destination", "aircraftType",
                "fareBasisCode", "fareFamily",
                "totalAmount", "baseFareAmount", "taxAmount",
                "isRefundable", "isChangeable",
                "pointsPrice", "pointsTaxes"
            })
            {
                if (offerObj[prop] is JsonNode val)
                    item[prop] = val.DeepClone();
            }
            flightOrderItems.Add(item);
        }

        var orderData = new JsonObject
        {
            ["currencyCode"] = basket.CurrencyCode,
            ["dataLists"] = new JsonObject
            {
                ["passengers"] = passengersNode.DeepClone()
            },
            ["orderItems"] = flightOrderItems,
            ["payments"] = paymentsNode?.DeepClone() ?? new JsonArray(),
            ["eTickets"] = new JsonArray(),
            ["seatAssignments"] = basketJson["seats"]?.DeepClone() ?? new JsonArray(),
            ["bagItems"] = basketJson["bags"]?.DeepClone() ?? new JsonArray(),
            ["ssrItems"] = basketJson["ssrSelections"]?.DeepClone() ?? new JsonArray(),
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
}
