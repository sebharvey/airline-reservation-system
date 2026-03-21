using ReservationSystem.Microservices.Order.Application.CreateBasket;
using ReservationSystem.Microservices.Order.Application.CreateOrder;
using ReservationSystem.Microservices.Order.Application.UpdateBasketBags;
using ReservationSystem.Microservices.Order.Application.UpdateBasketFlights;
using ReservationSystem.Microservices.Order.Application.UpdateBasketPassengers;
using ReservationSystem.Microservices.Order.Application.UpdateBasketSeats;
using ReservationSystem.Microservices.Order.Domain.Entities;
using ReservationSystem.Microservices.Order.Models.Requests;
using ReservationSystem.Microservices.Order.Models.Responses;

namespace ReservationSystem.Microservices.Order.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations of Order domain objects.
///
/// Mapping directions:
///   HTTP request  →  Application command/query
///   Domain entity →  HTTP response
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class OrderMapper
{
    // -------------------------------------------------------------------------
    // HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateBasketCommand ToCommand(CreateBasketRequest request) =>
        new(
            ChannelCode: request.ChannelCode,
            CurrencyCode: request.CurrencyCode,
            ExpiresAt: request.ExpiresAt);

    public static CreateOrderCommand ToCommand(CreateOrderRequest request) =>
        new(BasketId: request.BasketId);

    public static UpdateBasketFlightsCommand ToCommand(Guid basketId, UpdateBasketFlightsRequest request) =>
        new(BasketId: basketId, FlightsData: request.FlightsData);

    public static UpdateBasketSeatsCommand ToCommand(Guid basketId, UpdateBasketSeatsRequest request) =>
        new(BasketId: basketId, SeatsData: request.SeatsData);

    public static UpdateBasketBagsCommand ToCommand(Guid basketId, UpdateBasketBagsRequest request) =>
        new(BasketId: basketId, BagsData: request.BagsData);

    public static UpdateBasketPassengersCommand ToCommand(Guid basketId, UpdateBasketPassengersRequest request) =>
        new(BasketId: basketId, PassengersData: request.PassengersData);

    // -------------------------------------------------------------------------
    // Domain entity → HTTP response
    // -------------------------------------------------------------------------

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
            CreatedAt = basket.CreatedAt
        };

    public static OrderResponse ToResponse(Domain.Entities.Order order) =>
        new()
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
            UpdatedAt = order.UpdatedAt
        };

    public static CreateOrderResponse ToCreateResponse(Domain.Entities.Order order) =>
        new()
        {
            OrderId = order.OrderId,
            BookingReference = order.BookingReference,
            OrderStatus = order.OrderStatus,
            CreatedAt = order.CreatedAt
        };
}
