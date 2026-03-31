using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetOrder;

public sealed class GetOrderHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;

    public GetOrderHandler(OrderServiceClient orderServiceClient, OfferServiceClient offerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
    }

    public async Task<OrderResponse?> HandleRetrieveAsync(string bookingReference, string surname, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.RetrieveOrderAsync(bookingReference, surname, cancellationToken);
        if (order is null)
            return null;

        return await MapToResponseAsync(order, bookingReference, cancellationToken);
    }

    public async Task<OrderResponse?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByRefAsync(query.BookingReference, cancellationToken);
        if (order is null)
            return null;

        return await MapToResponseAsync(order, query.BookingReference, cancellationToken);
    }

    private async Task<OrderResponse> MapToResponseAsync(OrderMsOrderResult order, string fallbackBookingReference, CancellationToken cancellationToken)
    {
        var flights = new List<OrderFlight>();
        var passengers = new List<OrderPassenger>();
        var eTickets = new List<IssuedETicket>();

        if (order.OrderData.HasValue)
        {
            try
            {
                var data = order.OrderData.Value;
                if (data.TryGetProperty("dataLists", out var dataLists))
                {
                    if (dataLists.TryGetProperty("passengers", out var paxArray))
                    {
                        foreach (var pax in paxArray.EnumerateArray())
                        {
                            passengers.Add(new OrderPassenger
                            {
                                FirstName = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "",
                                LastName = pax.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "",
                            });
                        }
                    }
                }

                if (data.TryGetProperty("orderItems", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        // Resolve flight details from the Offer MS using inventoryId
                        if (item.TryGetProperty("inventoryId", out var invIdEl) &&
                            invIdEl.TryGetGuid(out var inventoryId))
                        {
                            var inv = await _offerServiceClient.GetFlightByInventoryIdAsync(inventoryId, cancellationToken);
                            if (inv is not null)
                            {
                                var departureDateTime = DateTime.TryParse(
                                    $"{inv.DepartureDate}T{inv.DepartureTime}:00Z", out var dep) ? dep : default;
                                var arrivalDateTime = DateTime.TryParse(
                                    $"{inv.DepartureDate}T{inv.ArrivalTime}:00Z", out var arr) ? arr : default;

                                flights.Add(new OrderFlight
                                {
                                    FlightNumber = inv.FlightNumber,
                                    Origin = inv.Origin,
                                    Destination = inv.Destination,
                                    DepartureTime = departureDateTime,
                                    ArrivalTime = arrivalDateTime,
                                    CabinClass = string.Empty,
                                });
                            }
                        }

                        var eTicket = item.TryGetProperty("eTicketNumber", out var et) ? et.GetString() : null;
                        if (!string.IsNullOrEmpty(eTicket))
                        {
                            eTickets.Add(new IssuedETicket
                            {
                                PassengerId = item.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "",
                                SegmentId = item.TryGetProperty("segmentId", out var sid) ? sid.GetString() ?? "" : "",
                                ETicketNumber = eTicket,
                            });
                        }
                    }
                }
            }
            catch { }
        }

        return new OrderResponse
        {
            BookingReference = order.BookingReference ?? fallbackBookingReference,
            Status = order.OrderStatus,
            Flights = flights,
            Passengers = passengers,
            ETickets = eTickets,
            TotalPrice = order.TotalAmount ?? 0m,
            Currency = order.CurrencyCode,
            BookedAt = order.CreatedAt,
        };
    }
}
