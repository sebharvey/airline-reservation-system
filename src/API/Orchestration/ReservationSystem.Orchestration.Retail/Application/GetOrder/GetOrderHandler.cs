using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetOrder;

public sealed class GetOrderHandler
{
    private readonly OrderServiceClient _orderServiceClient;

    public GetOrderHandler(OrderServiceClient orderServiceClient)
    {
        _orderServiceClient = orderServiceClient;
    }

    public async Task<OrderResponse?> HandleRetrieveAsync(string bookingReference, string surname, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.RetrieveOrderAsync(bookingReference, surname, cancellationToken);
        if (order is null)
            return null;

        return MapToResponse(order, bookingReference);
    }

    public async Task<OrderResponse?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByRefAsync(query.BookingReference, cancellationToken);
        if (order is null)
            return null;

        return MapToResponse(order, query.BookingReference);
    }

    private static OrderResponse MapToResponse(OrderMsOrderResult order, string fallbackBookingReference)
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

                    if (dataLists.TryGetProperty("flightSegments", out var segments))
                    {
                        foreach (var seg in segments.EnumerateArray())
                        {
                            DateTime.TryParse(
                                seg.TryGetProperty("departureTime", out var dt) ? dt.GetString() : null,
                                out var departure);
                            DateTime.TryParse(
                                seg.TryGetProperty("arrivalTime", out var at) ? at.GetString() : null,
                                out var arrival);

                            flights.Add(new OrderFlight
                            {
                                FlightNumber = seg.TryGetProperty("flightNumber", out var fn) ? fn.GetString() ?? "" : "",
                                Origin = seg.TryGetProperty("origin", out var orig) ? orig.GetString() ?? "" : "",
                                Destination = seg.TryGetProperty("destination", out var dst) ? dst.GetString() ?? "" : "",
                                DepartureTime = departure,
                                ArrivalTime = arrival,
                                CabinClass = seg.TryGetProperty("cabinClass", out var cc) ? cc.GetString() ?? "" : "",
                            });
                        }
                    }
                }

                if (data.TryGetProperty("orderItems", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
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
