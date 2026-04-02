using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.OciRetrieve;

public sealed class OciRetrieveHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;

    public OciRetrieveHandler(OrderServiceClient orderServiceClient, OfferServiceClient offerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
    }

    public async Task<OciOrderResponse?> HandleAsync(OciRetrieveQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.OciRetrieveOrderAsync(query.BookingReference, query.Surname, cancellationToken);
        if (order is null)
            return null;

        var passengers = new List<OciPassenger>();
        var flightSegments = new List<OciFlightSegment>();

        if (order.OrderData.HasValue)
        {
            try
            {
                var data = order.OrderData.Value;

                // Extract passengers from dataLists.passengers
                if (data.TryGetProperty("dataLists", out var dataLists) &&
                    dataLists.TryGetProperty("passengers", out var paxArray))
                {
                    foreach (var pax in paxArray.EnumerateArray())
                    {
                        passengers.Add(new OciPassenger
                        {
                            PassengerId = pax.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "",
                            Type = pax.TryGetProperty("type", out var t) ? t.GetString() ?? "ADT" : "ADT",
                            GivenName = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "",
                            Surname = pax.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "",
                        });
                    }
                }

                // Extract Flight order items and enrich with Offer MS inventory data
                if (data.TryGetProperty("orderItems", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var itemType = item.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                        if (itemType != "Flight") continue;

                        if (!item.TryGetProperty("inventoryId", out var invIdEl) || !invIdEl.TryGetGuid(out var inventoryId))
                            continue;

                        var inv = await _offerServiceClient.GetFlightByInventoryIdAsync(inventoryId, cancellationToken);
                        if (inv is null) continue;

                        var departureDateTime = DateTime.TryParse(
                            $"{inv.DepartureDate}T{inv.DepartureTime}:00Z", out var dep) ? dep : default;

                        var arrivalDate = inv.ArrivalDayOffset > 0
                            ? DateOnly.Parse(inv.DepartureDate).AddDays(inv.ArrivalDayOffset).ToString("yyyy-MM-dd")
                            : inv.DepartureDate;
                        var arrivalDateTime = DateTime.TryParse(
                            $"{arrivalDate}T{inv.ArrivalTime}:00Z", out var arr) ? arr : default;

                        var seatAssignments = new List<OciSeatAssignment>();
                        if (item.TryGetProperty("seatAssignments", out var seats))
                        {
                            foreach (var seat in seats.EnumerateArray())
                            {
                                var seatPaxId = seat.TryGetProperty("passengerId", out var spid) ? spid.GetString() ?? "" : "";
                                var seatNum = seat.TryGetProperty("seatNumber", out var sn2) ? sn2.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(seatPaxId) && !string.IsNullOrEmpty(seatNum))
                                    seatAssignments.Add(new OciSeatAssignment { PassengerId = seatPaxId, SeatNumber = seatNum });
                            }
                        }

                        flightSegments.Add(new OciFlightSegment
                        {
                            SegmentRef = item.TryGetProperty("segmentRef", out var sr) ? sr.GetString() ?? "" : "",
                            InventoryId = inventoryId,
                            FlightNumber = inv.FlightNumber,
                            Origin = inv.Origin,
                            Destination = inv.Destination,
                            DepartureDateTime = departureDateTime,
                            ArrivalDateTime = arrivalDateTime,
                            CabinCode = item.TryGetProperty("cabinCode", out var cc) ? cc.GetString() ?? "" : "",
                            AircraftType = inv.AircraftType,
                            SeatAssignments = seatAssignments,
                        });
                    }
                }
            }
            catch { }
        }

        return new OciOrderResponse
        {
            BookingReference = order.BookingReference ?? query.BookingReference,
            OrderStatus = order.OrderStatus,
            CurrencyCode = order.CurrencyCode,
            Passengers = passengers,
            FlightSegments = flightSegments,
        };
    }
}
