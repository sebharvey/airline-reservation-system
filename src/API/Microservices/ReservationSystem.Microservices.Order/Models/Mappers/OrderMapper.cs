using ReservationSystem.Microservices.Order.Application.ConfirmOrder;
using ReservationSystem.Microservices.Order.Application.CreateBasket;
using ReservationSystem.Microservices.Order.Application.CreateOrder;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Microservices.Order.Models.Responses;
using System.Text.Json;
using System.Collections.Generic;

namespace ReservationSystem.Microservices.Order.Models.Mappers;

public static class OrderMapper
{
    public static CreateBasketCommand ToCommand(CreateBasketRequest request) =>
        new(
            ChannelCode: request.ChannelCode,
            CurrencyCode: string.IsNullOrWhiteSpace(request.CurrencyCode) ? "GBP" : request.CurrencyCode,
            BookingType: string.IsNullOrWhiteSpace(request.BookingType) ? "Revenue" : request.BookingType,
            LoyaltyNumber: request.LoyaltyNumber,
            TotalPointsAmount: request.TotalPointsAmount);

    public static CreateOrderCommand ToCommand(CreateOrderRequest request) =>
        new(
            BasketId: request.BasketId,
            RedemptionReference: request.RedemptionReference,
            BookingType: string.IsNullOrWhiteSpace(request.BookingType) ? "Revenue" : request.BookingType);

    public static ConfirmOrderCommand ToConfirmCommand(ConfirmOrderRequest request) =>
        new(
            OrderId: request.OrderId,
            BasketId: request.BasketId,
            PaymentReferencesJson: request.PaymentReferences is not null
                ? JsonSerializer.Serialize(request.PaymentReferences)
                : "[]",
            EnrichedOffersJson: request.EnrichedOffers is not null
                ? JsonSerializer.Serialize(request.EnrichedOffers)
                : null);

    public static BasketResponse ToResponse(Basket basket) =>
        new()
        {
            BasketId = basket.BasketId,
            ChannelCode = basket.ChannelCode,
            CurrencyCode = basket.CurrencyCode,
            BasketStatus = basket.BasketStatus,
            TotalFareAmount = basket.TotalFareAmount,
            TotalSeatAmount = basket.TotalSeatAmount,
            TotalBagAmount = basket.TotalBagAmount,
            TotalAmount = basket.TotalAmount,
            ExpiresAt = basket.ExpiresAt,
            ConfirmedOrderId = basket.ConfirmedOrderId,
            Version = basket.Version,
            CreatedAt = basket.CreatedAt,
            UpdatedAt = basket.UpdatedAt,
            BasketData = basket.BasketData
        };

    public static CreateBasketResponse ToCreateResponse(Basket basket) =>
        new()
        {
            BasketId = basket.BasketId,
            BasketStatus = basket.BasketStatus,
            ExpiresAt = basket.ExpiresAt,
            TotalAmount = basket.TotalAmount ?? 0m,
            CurrencyCode = basket.CurrencyCode
        };

    public static OrderResponse ToResponse(Domain.Entities.Order order)
    {
        JsonElement? orderData = null;
        if (!string.IsNullOrWhiteSpace(order.OrderData) && order.OrderData != "{}")
        {
            try { orderData = JsonSerializer.Deserialize<JsonElement>(order.OrderData); }
            catch { }
        }

        return new()
        {
            OrderId = order.OrderId,
            BookingReference = order.BookingReference,
            OrderStatus = order.OrderStatus,
            ChannelCode = order.ChannelCode,
            CurrencyCode = order.CurrencyCode,
            TicketingTimeLimit = order.TicketingTimeLimit,
            TotalAmount = order.TotalAmount,
            Version = order.Version,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            OrderData = orderData
        };
    }

    public static CreateOrderResponse ToCreateResponse(Domain.Entities.Order order) =>
        new()
        {
            OrderId = order.OrderId,
            BookingReference = order.BookingReference,
            OrderStatus = order.OrderStatus,
            TotalAmount = order.TotalAmount,
            CurrencyCode = order.CurrencyCode
        };

    public static ConfirmOrderResponse ToConfirmResponse(Domain.Entities.Order order)
    {
        var orderItems = new List<ConfirmedOrderItem>();
        if (!string.IsNullOrWhiteSpace(order.OrderData) && order.OrderData != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(order.OrderData);
                if (doc.RootElement.TryGetProperty("orderItems", out var itemsEl) &&
                    itemsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsEl.EnumerateArray())
                    {
                        List<ConfirmedTaxLine>? taxLines = null;
                        if (item.TryGetProperty("taxLines", out var tlEl) &&
                            tlEl.ValueKind == JsonValueKind.Array)
                        {
                            taxLines = new List<ConfirmedTaxLine>();
                            foreach (var tl in tlEl.EnumerateArray())
                            {
                                taxLines.Add(new ConfirmedTaxLine
                                {
                                    Code        = tl.TryGetProperty("code",        out var c) ? c.GetString() ?? "" : "",
                                    Amount      = tl.TryGetProperty("amount",      out var a) ? a.GetDecimal()      : 0m,
                                    Description = tl.TryGetProperty("description", out var d) && d.ValueKind != JsonValueKind.Null ? d.GetString() : null
                                });
                            }
                        }

                        _ = item.TryGetProperty("offerId", out var oIdEl) && oIdEl.TryGetGuid(out var oId);
                        orderItems.Add(new ConfirmedOrderItem
                        {
                            OfferId        = oId,
                            FlightNumber   = item.TryGetProperty("flightNumber",   out var v) ? v.GetString() ?? "" : "",
                            Origin         = item.TryGetProperty("origin",         out v) ? v.GetString() ?? "" : "",
                            Destination    = item.TryGetProperty("destination",    out v) ? v.GetString() ?? "" : "",
                            DepartureDate  = item.TryGetProperty("departureDate",  out v) ? v.GetString() ?? "" : "",
                            DepartureTime  = item.TryGetProperty("departureTime",  out v) ? v.GetString() ?? "" : "",
                            ArrivalTime    = item.TryGetProperty("arrivalTime",    out v) ? v.GetString() ?? "" : "",
                            CabinCode      = item.TryGetProperty("cabinCode",      out v) ? v.GetString() ?? "" : "",
                            FareFamily     = item.TryGetProperty("fareFamily",     out v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null,
                            FareBasisCode  = item.TryGetProperty("fareBasisCode",  out v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null,
                            BaseFareAmount = item.TryGetProperty("baseFareAmount", out v) ? v.GetDecimal() : 0m,
                            TaxAmount      = item.TryGetProperty("taxAmount",      out v) ? v.GetDecimal() : 0m,
                            TotalAmount    = item.TryGetProperty("totalAmount",    out v) ? v.GetDecimal() : 0m,
                            TaxLines       = taxLines
                        });
                    }
                }
            }
            catch { /* Return without order items if parse fails */ }
        }

        return new()
        {
            OrderId        = order.OrderId,
            BookingReference = order.BookingReference ?? string.Empty,
            OrderStatus    = order.OrderStatus,
            TotalAmount    = order.TotalAmount,
            CurrencyCode   = order.CurrencyCode,
            OrderItems     = orderItems
        };
    }
}
