using System.Text.Json;
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

    public async Task<ManagedOrderResponse?> HandleRetrieveAsync(
        string bookingReference, string surname, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.RetrieveOrderAsync(bookingReference, surname, cancellationToken);
        return order is null ? null : MapToResponse(order);
    }

    public async Task<ManagedOrderResponse?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByRefAsync(query.BookingReference, cancellationToken);
        return order is null ? null : MapToResponse(order);
    }

    private static ManagedOrderResponse MapToResponse(OrderMsOrderResult order)
    {
        var passengers = new List<ManagedPassenger>();
        var flightSegments = new List<ManagedFlightSegment>();
        var orderItems = new List<ManagedOrderItem>();
        var payments = new List<ManagedPayment>();
        ManagedPointsRedemption? pointsRedemption = null;
        int? totalPointsAmount = null;

        if (order.OrderData.HasValue)
        {
            try
            {
                var data = order.OrderData.Value;

                // ── Passengers ────────────────────────────────────────────────
                if (data.TryGetProperty("dataLists", out var dataLists) &&
                    dataLists.TryGetProperty("passengers", out var paxArray))
                {
                    foreach (var pax in paxArray.EnumerateArray())
                    {
                        ManagedPassengerContacts? contacts = null;
                        if (pax.TryGetProperty("contacts", out var contactsEl) &&
                            contactsEl.ValueKind == JsonValueKind.Object)
                        {
                            contacts = new ManagedPassengerContacts
                            {
                                Email = contactsEl.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "",
                                Phone = contactsEl.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : ""
                            };
                        }

                        ManagedTravelDocument? travelDoc = null;
                        if (pax.TryGetProperty("travelDocument", out var docEl) &&
                            docEl.ValueKind == JsonValueKind.Object)
                        {
                            travelDoc = new ManagedTravelDocument
                            {
                                Type = docEl.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                                Number = docEl.TryGetProperty("number", out var n) ? n.GetString() ?? "" : "",
                                IssuingCountry = docEl.TryGetProperty("issuingCountry", out var ic) ? ic.GetString() ?? "" : "",
                                ExpiryDate = docEl.TryGetProperty("expiryDate", out var ed) ? ed.GetString() ?? "" : "",
                                Nationality = docEl.TryGetProperty("nationality", out var nat) ? nat.GetString() ?? "" : ""
                            };
                        }

                        passengers.Add(new ManagedPassenger
                        {
                            PassengerId = pax.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "",
                            Type = pax.TryGetProperty("type", out var type) ? type.GetString() ?? "ADT" : "ADT",
                            GivenName = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "",
                            Surname = pax.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "",
                            DateOfBirth = pax.TryGetProperty("dateOfBirth", out var dob) ? dob.GetString() ?? "" : "",
                            Gender = pax.TryGetProperty("gender", out var gen) ? gen.GetString() ?? "" : "",
                            LoyaltyNumber = pax.TryGetProperty("loyaltyNumber", out var ln) ? ln.GetString() : null,
                            Contacts = contacts,
                            TravelDocument = travelDoc
                        });
                    }
                }

                // ── eTickets lookup ───────────────────────────────────────────
                // eTickets stored as: [{ passengerId, segmentIds[], eTicketNumber }]
                // or: [{ passengerId, eTicketNumber }] (older format)
                var eTicketsBySegment = new Dictionary<string, List<ManagedETicket>>(StringComparer.OrdinalIgnoreCase);
                if (data.TryGetProperty("eTickets", out var eTicketsArray) &&
                    eTicketsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var et in eTicketsArray.EnumerateArray())
                    {
                        var paxId = et.TryGetProperty("passengerId", out var epid) ? epid.GetString() ?? "" : "";
                        var etNum = et.TryGetProperty("eTicketNumber", out var etn) ? etn.GetString() ?? "" : "";
                        var managedEt = new ManagedETicket { PassengerId = paxId, ETicketNumber = etNum };

                        if (et.TryGetProperty("segmentIds", out var segIds) &&
                            segIds.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var sid in segIds.EnumerateArray())
                            {
                                var segIdStr = sid.GetString() ?? "";
                                if (!eTicketsBySegment.ContainsKey(segIdStr))
                                    eTicketsBySegment[segIdStr] = new List<ManagedETicket>();
                                eTicketsBySegment[segIdStr].Add(managedEt);
                            }
                        }
                    }
                }

                // ── Seat assignments lookup ───────────────────────────────────
                // seatAssignments stored as: [{ passengerId, segmentId, seatNumber }]
                var seatsBySegment = new Dictionary<string, List<ManagedSeatAssignment>>(StringComparer.OrdinalIgnoreCase);
                JsonElement seatsSource;
                var hasSeats = data.TryGetProperty("seatAssignments", out seatsSource) ||
                               data.TryGetProperty("seats", out seatsSource);
                if (hasSeats && seatsSource.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sa in seatsSource.EnumerateArray())
                    {
                        var paxId = sa.TryGetProperty("passengerId", out var spid) ? spid.GetString() ?? "" : "";
                        var segId = sa.TryGetProperty("segmentId", out var ssid) ? ssid.GetString() ?? "" : "";
                        var seatNum = sa.TryGetProperty("seatNumber", out var sn) ? sn.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(segId))
                        {
                            if (!seatsBySegment.ContainsKey(segId))
                                seatsBySegment[segId] = new List<ManagedSeatAssignment>();
                            seatsBySegment[segId].Add(new ManagedSeatAssignment { PassengerId = paxId, SeatNumber = seatNum });
                        }
                    }
                }

                // ── Payments ──────────────────────────────────────────────────
                if (data.TryGetProperty("payments", out var paymentsArray) &&
                    paymentsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in paymentsArray.EnumerateArray())
                    {
                        payments.Add(new ManagedPayment
                        {
                            PaymentReference = p.TryGetProperty("paymentReference", out var pr) ? pr.GetString() ?? "" : "",
                            Description = p.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                            Method = p.TryGetProperty("method", out var meth) ? meth.GetString() ?? "" : "",
                            CardLast4 = p.TryGetProperty("cardLast4", out var cl4) ? cl4.GetString() ?? "" : "",
                            CardType = p.TryGetProperty("cardType", out var ct) ? ct.GetString() ?? "" : "",
                            AuthorisedAmount = p.TryGetProperty("authorisedAmount", out var amt) ? amt.GetDecimal() : 0m,
                            SettledAmount = p.TryGetProperty("settledAmount", out var samt) ? samt.GetDecimal() : 0m,
                            Currency = p.TryGetProperty("currency", out var cur) ? cur.GetString() ?? order.CurrencyCode : order.CurrencyCode,
                            Status = p.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                            AuthorisedAt = p.TryGetProperty("authorisedAt", out var aat) ? aat.GetString() : null,
                            SettledAt = p.TryGetProperty("settledAt", out var sat) ? sat.GetString() : null
                        });
                    }
                }

                // ── Points redemption ─────────────────────────────────────────
                if (data.TryGetProperty("pointsRedemption", out var redemption) &&
                    redemption.ValueKind == JsonValueKind.Object)
                {
                    var pts = redemption.TryGetProperty("pointsRedeemed", out var pr) ? pr.GetInt32() : 0;
                    totalPointsAmount = pts;
                    pointsRedemption = new ManagedPointsRedemption
                    {
                        RedemptionReference = redemption.TryGetProperty("redemptionReference", out var rr) ? rr.GetString() ?? "" : "",
                        LoyaltyNumber = redemption.TryGetProperty("loyaltyNumber", out var ln) ? ln.GetString() ?? "" : "",
                        PointsRedeemed = pts,
                        Status = redemption.TryGetProperty("status", out var st) ? st.GetString() ?? "" : ""
                    };
                }

                // ── Order items → flight segments + order items ───────────────
                var paxIds = passengers.Select(p => p.PassengerId).ToList();

                if (data.TryGetProperty("orderItems", out var itemsArray) &&
                    itemsArray.ValueKind == JsonValueKind.Array)
                {
                    var itemIndex = 1;
                    foreach (var item in itemsArray.EnumerateArray())
                    {
                        var inventoryId = item.TryGetProperty("inventoryId", out var invId) ? invId.GetString() ?? "" : "";
                        var basketItemId = item.TryGetProperty("basketItemId", out var bid) ? bid.GetString() : null;
                        var segmentId = basketItemId ?? (string.IsNullOrEmpty(inventoryId) ? $"SEG-{itemIndex}" : inventoryId);

                        var flightNumber = item.TryGetProperty("flightNumber", out var fn) ? fn.GetString() ?? "" : "";
                        var departureDate = item.TryGetProperty("departureDate", out var dd) ? dd.GetString() ?? "" : "";
                        var departureTime = item.TryGetProperty("departureTime", out var dt) ? dt.GetString() ?? "00:00" : "00:00";
                        var arrivalTime = item.TryGetProperty("arrivalTime", out var at) ? at.GetString() ?? "00:00" : "00:00";
                        var origin = item.TryGetProperty("origin", out var orig) ? orig.GetString() ?? "" : "";
                        var destination = item.TryGetProperty("destination", out var dest) ? dest.GetString() ?? "" : "";
                        var aircraftType = item.TryGetProperty("aircraftType", out var act) ? act.GetString() ?? "" : "";
                        var cabinCode = item.TryGetProperty("cabinCode", out var cc) ? cc.GetString() ?? "Y" : "Y";
                        var fareBasisCode = item.TryGetProperty("fareBasisCode", out var fbc) ? fbc.GetString() : null;
                        var fareFamily = item.TryGetProperty("fareFamily", out var ff) ? ff.GetString() : null;
                        var totalAmount = item.TryGetProperty("totalAmount", out var ta) ? ta.GetDecimal() : 0m;
                        var baseFare = item.TryGetProperty("baseFareAmount", out var bf) ? bf.GetDecimal() : totalAmount;
                        var taxAmount = item.TryGetProperty("taxAmount", out var tax) ? tax.GetDecimal() : 0m;
                        var isRefundable = item.TryGetProperty("isRefundable", out var ir) && ir.GetBoolean();
                        var isChangeable = item.TryGetProperty("isChangeable", out var ic) && ic.GetBoolean();

                        // Build ISO 8601 departure/arrival datetime strings
                        var depDateTime = BuildDateTime(departureDate, departureTime);
                        var arrDateTime = BuildDateTime(departureDate, arrivalTime);

                        // Flight segment
                        flightSegments.Add(new ManagedFlightSegment
                        {
                            SegmentId = segmentId,
                            FlightNumber = flightNumber,
                            Origin = origin,
                            Destination = destination,
                            DepartureDateTime = depDateTime,
                            ArrivalDateTime = arrDateTime,
                            AircraftType = aircraftType,
                            OperatingCarrier = "AX",
                            MarketingCarrier = "AX",
                            CabinCode = cabinCode,
                            BookingClass = fareBasisCode?.Length > 0 ? fareBasisCode[0].ToString() : cabinCode
                        });

                        // e-tickets for this segment
                        eTicketsBySegment.TryGetValue(segmentId, out var segETickets);
                        // Seat assignments are keyed by inventoryId (the web app stores
                        // inventoryId as the seat's segmentId). The order item's segmentId
                        // is basketItemId ("BI-1" etc.), so we must fall back to inventoryId.
                        if (!seatsBySegment.TryGetValue(segmentId, out var seatsFound) &&
                            !seatsBySegment.TryGetValue(inventoryId, out seatsFound))
                        {
                            seatsFound = new List<ManagedSeatAssignment>();
                        }
                        var segSeatAssignments = seatsFound ?? new List<ManagedSeatAssignment>();

                        // Flight order item
                        orderItems.Add(new ManagedOrderItem
                        {
                            OrderItemId = segmentId,
                            Type = "Flight",
                            SegmentRef = segmentId,
                            PassengerRefs = paxIds,
                            FareFamily = fareFamily,
                            FareBasisCode = fareBasisCode,
                            UnitPrice = baseFare,
                            Taxes = taxAmount,
                            TotalPrice = totalAmount,
                            IsRefundable = isRefundable,
                            IsChangeable = isChangeable,
                            PaymentReference = payments.FirstOrDefault()?.PaymentReference ?? "",
                            ETickets = segETickets ?? [],
                            SeatAssignments = segSeatAssignments
                        });

                        itemIndex++;
                    }
                }

                // ── Bag items ─────────────────────────────────────────────────
                JsonElement bagSource;
                var hasBags = data.TryGetProperty("bagItems", out bagSource) ||
                              data.TryGetProperty("bags", out bagSource);
                if (hasBags && bagSource.ValueKind == JsonValueKind.Array)
                {
                    foreach (var bag in bagSource.EnumerateArray())
                    {
                        var paxId = bag.TryGetProperty("passengerId", out var bpid) ? bpid.GetString() ?? "" : "";
                        var segId = bag.TryGetProperty("segmentId", out var bsid) ? bsid.GetString() ?? "" : "";
                        var addBags = bag.TryGetProperty("additionalBags", out var ab) ? ab.GetInt32() : 1;
                        var bagPrice = bag.TryGetProperty("price", out var bp) ? bp.GetDecimal() : 0m;

                        orderItems.Add(new ManagedOrderItem
                        {
                            OrderItemId = $"bag-{paxId}-{segId}",
                            Type = "Bag",
                            SegmentRef = segId,
                            PassengerRefs = [paxId],
                            UnitPrice = bagPrice,
                            Taxes = 0m,
                            TotalPrice = bagPrice,
                            IsRefundable = false,
                            IsChangeable = false,
                            AdditionalBags = addBags,
                            PaymentReference = "",
                            ETickets = [],
                            SeatAssignments = []
                        });
                    }
                }
            }
            catch { /* return partial data */ }
        }

        return new ManagedOrderResponse
        {
            OrderId = order.OrderId.ToString(),
            BookingReference = order.BookingReference ?? "",
            OrderStatus = order.OrderStatus,
            BookingType = GetBookingType(order),
            ChannelCode = order.ChannelCode,
            CurrencyCode = order.CurrencyCode,
            TotalAmount = order.TotalAmount ?? 0m,
            TotalPointsAmount = totalPointsAmount,
            CreatedAt = order.CreatedAt,
            Passengers = passengers,
            FlightSegments = flightSegments,
            OrderItems = orderItems,
            Payments = payments,
            PointsRedemption = pointsRedemption
        };
    }

    private static string GetBookingType(OrderMsOrderResult order)
    {
        if (!order.OrderData.HasValue) return "Revenue";
        try
        {
            var data = order.OrderData.Value;
            if (data.TryGetProperty("bookingType", out var bt))
                return bt.GetString() ?? "Revenue";
        }
        catch { }
        return "Revenue";
    }

    private static string BuildDateTime(string date, string time)
    {
        if (string.IsNullOrEmpty(date)) return "";
        // time may be "HH:mm" or "HH:mm:ss"
        var t = time.Contains(':') ? time : "00:00";
        if (!t.Contains(':')) t = "00:00";
        return $"{date}T{t}:00Z";
    }
}
