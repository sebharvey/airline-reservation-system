using ReservationSystem.Microservices.Order.Application.CreateBasket;
using ReservationSystem.Microservices.Order.Application.CreateOrder;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Microservices.Order.Models.Responses;
using System.Text.Json;

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
            ETicketsJson: request.ETickets is not null ? JsonSerializer.Serialize(request.ETickets) : "[]",
            PaymentReferencesJson: request.PaymentReferences is not null ? JsonSerializer.Serialize(request.PaymentReferences) : "[]",
            RedemptionReference: request.RedemptionReference,
            BookingType: string.IsNullOrWhiteSpace(request.BookingType) ? "Revenue" : request.BookingType);

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
            UpdatedAt = basket.UpdatedAt
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
}
