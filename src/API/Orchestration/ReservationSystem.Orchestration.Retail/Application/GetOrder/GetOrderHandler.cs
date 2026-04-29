using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetOrder;

public sealed class GetOrderHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;

    public GetOrderHandler(
        OrderServiceClient orderServiceClient,
        PaymentServiceClient paymentServiceClient,
        DeliveryServiceClient deliveryServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _paymentServiceClient = paymentServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
    }

    /// <summary>
    /// Retrieves an order by booking reference using a pre-validated manage-booking JWT.
    /// Includes full ticket data from the Delivery MS.
    /// </summary>
    public async Task<ManagedOrderResponse?> HandleRetrieveAsync(
        string bookingReference, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByRefAsync(bookingReference, cancellationToken);
        if (order is null) return null;

        var tickets = await FetchTicketsAsync(bookingReference, cancellationToken);
        return await MapToResponseAsync(order, tickets, cancellationToken);
    }

    public async Task<ManagedOrderResponse?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.GetOrderByRefAsync(query.BookingReference, cancellationToken);
        return order is null ? null : await MapToResponseAsync(order, [], cancellationToken);
    }

    private async Task<IReadOnlyList<ManagedTicket>> FetchTicketsAsync(
        string bookingReference, CancellationToken cancellationToken)
    {
        try
        {
            var records = await _deliveryServiceClient.GetTicketsByBookingAsync(bookingReference, cancellationToken);
            return records.Select(t => new ManagedTicket
            {
                TicketId        = t.TicketId,
                ETicketNumber   = t.ETicketNumber,
                BookingReference = t.BookingReference,
                PassengerId     = t.PassengerId,
                IsVoided        = t.IsVoided,
                VoidedAt        = t.VoidedAt,
                TicketData      = t.TicketData.HasValue ? t.TicketData.Value : null,
                CreatedAt       = t.CreatedAt,
                UpdatedAt       = t.UpdatedAt,
                Version         = t.Version
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<ManagedOrderResponse> MapToResponseAsync(OrderMsOrderResult order, IReadOnlyList<ManagedTicket> tickets, CancellationToken cancellationToken)
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

                        var travelDocs = new List<ManagedTravelDocument>();
                        if (pax.TryGetProperty("docs", out var docsEl) &&
                            docsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var docEl in docsEl.EnumerateArray())
                            {
                                travelDocs.Add(new ManagedTravelDocument
                                {
                                    Type = docEl.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                                    Number = docEl.TryGetProperty("number", out var n) ? n.GetString() ?? "" : "",
                                    IssuingCountry = docEl.TryGetProperty("issuingCountry", out var ic) ? ic.GetString() ?? "" : "",
                                    ExpiryDate = docEl.TryGetProperty("expiryDate", out var ed) ? ed.GetString() ?? "" : "",
                                    Nationality = docEl.TryGetProperty("nationality", out var nat) ? nat.GetString() ?? "" : ""
                                });
                            }
                        }

                        passengers.Add(new ManagedPassenger
                        {
                            PassengerId = pax.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "",
                            Type = pax.TryGetProperty("type", out var type) ? type.GetString() ?? "ADT" : "ADT",
                            GivenName = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "",
                            Surname = pax.TryGetProperty("surname", out var sn) ? sn.GetString() ?? "" : "",
                            Dob = pax.TryGetProperty("dob", out var dob) ? dob.GetString() ?? "" : "",
                            Gender = pax.TryGetProperty("gender", out var gen) ? gen.GetString() ?? "" : "",
                            LoyaltyNumber = pax.TryGetProperty("loyaltyNumber", out var ln) ? ln.GetString() : null,
                            Contacts = contacts,
                            Docs = travelDocs
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

                // ── Payments ──────────────────────────────────────────────────
                if (data.TryGetProperty("payments", out var paymentsArray) &&
                    paymentsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in paymentsArray.EnumerateArray())
                    {
                        var paymentRef = p.TryGetProperty("paymentReference", out var pr) ? pr.GetString() ?? "" : "";

                        IReadOnlyList<ManagedPaymentEvent> events = [];
                        if (!string.IsNullOrEmpty(paymentRef))
                        {
                            try
                            {
                                var rawEvents = await _paymentServiceClient.GetPaymentEventsAsync(paymentRef, cancellationToken);
                                events = rawEvents.Select(e => new ManagedPaymentEvent
                                {
                                    PaymentEventId = e.PaymentEventId.ToString(),
                                    EventType      = e.EventType,
                                    ProductType    = e.ProductType,
                                    Amount         = e.Amount,
                                    Currency       = e.CurrencyCode,
                                    Notes          = e.Notes,
                                    CreatedAt      = e.CreatedAt
                                }).ToList();
                            }
                            catch { /* Non-fatal: return payment without events */ }
                        }

                        payments.Add(new ManagedPayment
                        {
                            PaymentReference = paymentRef,
                            Description = p.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                            Method = p.TryGetProperty("method", out var meth) ? meth.GetString() ?? "" : "",
                            CardLast4 = p.TryGetProperty("cardLast4", out var cl4) ? cl4.GetString() ?? "" : "",
                            CardType = p.TryGetProperty("cardType", out var ct) ? ct.GetString() ?? "" : "",
                            AuthorisedAmount = p.TryGetProperty("authorisedAmount", out var amt) ? amt.GetDecimal() : 0m,
                            SettledAmount = p.TryGetProperty("settledAmount", out var samt) ? samt.GetDecimal() : 0m,
                            Currency = p.TryGetProperty("currency", out var cur) ? cur.GetString() ?? order.CurrencyCode : order.CurrencyCode,
                            Status = p.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                            AuthorisedAt = p.TryGetProperty("authorisedAt", out var aat) ? aat.GetString() : null,
                            SettledAt = p.TryGetProperty("settledAt", out var sat) ? sat.GetString() : null,
                            Events = events
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
                        var productType = item.TryGetProperty("productType", out var ptEl) ? ptEl.GetString() : null;

                        // ── Seat items ────────────────────────────────────────
                        if (string.Equals(productType, "SEAT", StringComparison.OrdinalIgnoreCase))
                        {
                            var paxId  = item.TryGetProperty("passengerId", out var spid) ? spid.GetString() ?? "" : "";
                            var segId  = item.TryGetProperty("segmentId",   out var ssid) ? ssid.GetString() ?? "" : "";
                            var seatNum = item.TryGetProperty("seatNumber", out var sn)   ? sn.GetString()   ?? "" : "";
                            var price  = item.TryGetProperty("price",       out var pr)   ? pr.GetDecimal()       : 0m;
                            orderItems.Add(new ManagedOrderItem
                            {
                                OrderItemId    = $"seat-{paxId}-{segId}",
                                Type           = "Seat",
                                SegmentRef     = segId,
                                PassengerRefs  = string.IsNullOrEmpty(paxId) ? [] : [paxId],
                                UnitPrice      = price,
                                Taxes          = 0m,
                                TotalPrice     = price,
                                IsRefundable   = false,
                                IsChangeable   = false,
                                PaymentReference = payments.FirstOrDefault()?.PaymentReference ?? "",
                                ETickets       = [],
                                SeatNumber     = seatNum
                            });
                            itemIndex++;
                            continue;
                        }

                        // Only build flight segments from FLIGHT items — bags, products, and
                        // services are handled in the second pass below.
                        if (!string.Equals(productType, "FLIGHT", StringComparison.OrdinalIgnoreCase))
                        {
                            itemIndex++;
                            continue;
                        }

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
                            ETickets = segETickets ?? []
                        });

                        itemIndex++;
                    }
                }

                // ── Bag items (stored as productType=BAG within orderItems) ──
                if (data.TryGetProperty("orderItems", out var oiEl) && oiEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var oi in oiEl.EnumerateArray())
                    {
                        var oiProductType = oi.TryGetProperty("productType", out var ptEl) ? ptEl.GetString() : null;

                        if (string.Equals(oiProductType, "BAG", StringComparison.OrdinalIgnoreCase))
                        {
                            var paxId    = oi.TryGetProperty("passengerId",    out var bpid) ? bpid.GetString() ?? "" : "";
                            var segId    = oi.TryGetProperty("segmentId",      out var bsid) ? bsid.GetString() ?? "" : "";
                            var addBags  = oi.TryGetProperty("additionalBags", out var ab)   ? ab.GetInt32()          : 1;
                            var bagPrice = oi.TryGetProperty("price",          out var bp)   ? bp.GetDecimal()        : 0m;

                            orderItems.Add(new ManagedOrderItem
                            {
                                OrderItemId      = $"bag-{paxId}-{segId}",
                                Type             = "Bag",
                                SegmentRef       = segId,
                                PassengerRefs    = [paxId],
                                UnitPrice        = bagPrice,
                                Taxes            = 0m,
                                TotalPrice       = bagPrice,
                                IsRefundable     = false,
                                IsChangeable     = false,
                                AdditionalBags   = addBags,
                                PaymentReference = "",
                                ETickets         = []
                            });
                        }
                        else if (string.Equals(oiProductType, "PRODUCT", StringComparison.OrdinalIgnoreCase))
                        {
                            var paxId     = oi.TryGetProperty("passengerId", out var ppid) ? ppid.GetString() ?? "" : "";
                            var productId = oi.TryGetProperty("productId",   out var pid)  ? pid.GetString()  ?? "" : "";
                            var segRef    = oi.TryGetProperty("segmentRef",  out var psr)  ? psr.GetString()  ?? "" : "";
                            var price     = oi.TryGetProperty("price",       out var pp)   ? pp.GetDecimal()       : 0m;
                            var tax       = oi.TryGetProperty("tax",         out var pt)   ? pt.GetDecimal()       : 0m;

                            orderItems.Add(new ManagedOrderItem
                            {
                                OrderItemId      = $"product-{paxId}-{productId}-{segRef}",
                                Type             = "Product",
                                SegmentRef       = segRef,
                                PassengerRefs    = string.IsNullOrEmpty(paxId) ? [] : [paxId],
                                UnitPrice        = price,
                                Taxes            = tax,
                                TotalPrice       = price + tax,
                                IsRefundable     = false,
                                IsChangeable     = false,
                                PaymentReference = payments.FirstOrDefault()?.PaymentReference ?? "",
                                ETickets         = []
                            });
                        }
                        else if (string.Equals(oiProductType, "SERVICE", StringComparison.OrdinalIgnoreCase))
                        {
                            var ssrCode    = oi.TryGetProperty("ssrCode",      out var sc)   ? sc.GetString()  ?? "" : "";
                            var passengerRef = oi.TryGetProperty("passengerRef", out var pr)  ? pr.GetString()  ?? "" : "";
                            var segmentRef = oi.TryGetProperty("segmentRef",   out var sr)   ? sr.GetString()  ?? "" : "";

                            orderItems.Add(new ManagedOrderItem
                            {
                                OrderItemId      = $"ssr-{ssrCode}-{passengerRef}-{segmentRef}",
                                Type             = "SSR",
                                SegmentRef       = segmentRef,
                                PassengerRefs    = string.IsNullOrEmpty(passengerRef) ? [] : [passengerRef],
                                SsrCode          = ssrCode,
                                UnitPrice        = 0m,
                                Taxes            = 0m,
                                TotalPrice       = 0m,
                                IsRefundable     = false,
                                IsChangeable     = false,
                                PaymentReference = "",
                                ETickets         = []
                            });
                        }
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
            Currency = order.CurrencyCode,
            TotalAmount = order.TotalAmount ?? 0m,
            TotalPointsAmount = totalPointsAmount,
            CreatedAt = order.CreatedAt,
            Passengers = passengers,
            FlightSegments = flightSegments,
            OrderItems = orderItems,
            Payments = payments,
            PointsRedemption = pointsRedemption,
            Tickets = tickets
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
