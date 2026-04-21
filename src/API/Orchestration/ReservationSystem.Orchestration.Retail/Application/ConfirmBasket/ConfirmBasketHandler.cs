using System.Text;
using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Responses;
using ReservationSystem.Shared.Common.Json;
using System.Linq;

namespace ReservationSystem.Orchestration.Retail.Application.ConfirmBasket;

public sealed class ConfirmBasketHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly OfferServiceClient _offerServiceClient;
    private readonly PaymentServiceClient _paymentServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public ConfirmBasketHandler(
        OrderServiceClient orderServiceClient,
        OfferServiceClient offerServiceClient,
        PaymentServiceClient paymentServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _orderServiceClient = orderServiceClient;
        _offerServiceClient = offerServiceClient;
        _paymentServiceClient = paymentServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public async Task<OrderResponse> HandleAsync(ConfirmBasketCommand command, CancellationToken cancellationToken)
    {
        // 1. Retrieve and validate basket
        var basket = await _orderServiceClient.GetBasketAsync(command.BasketId, cancellationToken)
            ?? throw new InvalidOperationException($"Basket {command.BasketId} not found.");

        if (basket.BasketStatus != "Active")
            throw new InvalidOperationException($"Basket is not active. Current status: {basket.BasketStatus}");

        if (basket.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Basket has expired.");

        // Reprice and validate all offers before creating any order record.
        // Calling reprice (not just get) ensures we have the latest dynamic fares and tax lines.
        var basketDataJsonEarly = basket.BasketData.HasValue ? basket.BasketData.Value.GetRawText() : null;
        var repricedOffers = await RepriceAndValidateOffersAsync(basketDataJsonEarly, cancellationToken);

        var seatAmount    = ParseSeatAmountFromBasketData(basketDataJsonEarly);
        var bagAmount     = ParseBagAmountFromBasketData(basketDataJsonEarly);
        var productAmount = ParseProductAmountFromBasketData(basketDataJsonEarly);
        // Prices are locked at search time in the stored offer snapshot (CLAUDE.md rule #3).
        // Reprice is called for validation only — basket amounts are authoritative for charging.
        var totalAmount = CalculateTotalFromBasket(basketDataJsonEarly);
        var fareAmount  = totalAmount - seatAmount - bagAmount - productAmount;
        var currency = basket.CurrencyCode;
        var bookingType = string.Equals(command.BookingType, "Standby", StringComparison.OrdinalIgnoreCase)
            ? "Standby"
            : command.LoyaltyPointsToRedeem.HasValue ? "Reward" : "Revenue";

        // 2. Create draft order in Order MS — no booking reference yet, basket remains active
        var draftOrder = await _orderServiceClient.CreateOrderAsync(
            command.BasketId,
            channelCode: command.ChannelCode,
            bookingType: bookingType,
            redemptionReference: null,
            cancellationToken);

        // 3. Initialise one payment record for the full booking amount. Ancillary types
        //    are broken into sequential auth/settle PaymentEvent pairs on the same record.
        var paymentId = await _paymentServiceClient.InitialiseAsync(
            method: command.PaymentMethod,
            currencyCode: currency,
            amount: totalAmount,
            description: $"Booking payment — basket {command.BasketId}",
            cancellationToken);

        try
        {
            await _paymentServiceClient.AuthoriseAsync(paymentId, "Fare", fareAmount, command.CardNumber, command.ExpiryDate, command.Cvv, command.CardholderName, cancellationToken);
        }
        catch
        {
            await _paymentServiceClient.VoidAsync(paymentId, "PaymentAuthorisationFailure", cancellationToken);
            await _orderServiceClient.DeleteDraftOrderAsync(draftOrder.OrderId, cancellationToken);
            throw;
        }

        // 4. Confirm order in Order MS — validates completeness, assigns booking reference,
        //    writes payment references into OrderData, and deletes the basket.
        //    Pass enriched offer data so the Order MS can lock confirmed fares + tax lines.
        var paymentRefs = new List<object>
        {
            new { paymentReference = paymentId }
        };

        var enrichedOffers = BuildEnrichedOffersPayload(basketDataJsonEarly, repricedOffers);

        OrderMsConfirmOrderResult confirmedOrder;
        try
        {
            confirmedOrder = await _orderServiceClient.ConfirmOrderAsync(
                draftOrder.OrderId,
                command.BasketId,
                paymentRefs,
                cancellationToken,
                enrichedOffers: enrichedOffers.Count > 0 ? enrichedOffers : null);
        }
        catch
        {
            try { await _paymentServiceClient.VoidAsync(paymentId, "OrderConfirmationFailure", cancellationToken); } catch { }
            await _orderServiceClient.DeleteDraftOrderAsync(draftOrder.OrderId, cancellationToken);
            throw;
        }

        // 4b. Link booking reference to the payment record now that the order is confirmed.
        try { await _paymentServiceClient.UpdateBookingReferenceAsync(paymentId, confirmedOrder.BookingReference, cancellationToken); } catch { }

        // 5–8. Run post-confirm operations in parallel:
        //   - Hold + sell inventory (holds run in parallel across segments, then sell)
        //   - Issue e-tickets + write back to order
        //   - Settle payment
        //   - Link order to customer loyalty account
        var basketDataJson = basket.BasketData.HasValue ? basket.BasketData.Value.GetRawText() : null;

        var inventoryTask   = RunInventorySellAsync(basketDataJson, draftOrder.OrderId, command.BasketId, bookingType == "Standby", cancellationToken);
        var ticketsTask     = RunTicketIssuanceAsync(basketDataJson, command, paymentId, fareAmount, currency, confirmedOrder, repricedOffers, cancellationToken);
        var settleTask      = RunSettleAndAuthAncillariesAsync(paymentId, fareAmount, seatAmount, bagAmount, productAmount, command.CardNumber, command.ExpiryDate, command.Cvv, command.CardholderName, cancellationToken);
        var customerTask    = RunCustomerLinkAsync(basketDataJson, confirmedOrder, cancellationToken);
        var seatEmdTask     = seatAmount    > 0 ? RunSeatEmdIssuanceAsync(basketDataJson,    confirmedOrder.BookingReference, paymentId, cancellationToken) : Task.CompletedTask;
        var bagEmdTask      = bagAmount     > 0 ? RunBagEmdIssuanceAsync(basketDataJson,     confirmedOrder.BookingReference, paymentId, cancellationToken) : Task.CompletedTask;
        var productEmdTask  = productAmount > 0 ? RunProductEmdIssuanceAsync(basketDataJson, confirmedOrder.BookingReference, paymentId, cancellationToken) : Task.CompletedTask;

        await Task.WhenAll(inventoryTask, ticketsTask, settleTask, customerTask, seatEmdTask, bagEmdTask, productEmdTask);

        var issuedTickets = await ticketsTask;

        var bookedAt = DateTime.UtcNow.ToString("o");
        var confirmedTotalAmount = totalAmount;
        var cardInfo = ExtractCardInfo(command.CardNumber, command.CardholderName);

        return BuildOrderResponse(
            confirmedOrder, basketDataJson, command, paymentId, cardInfo,
            bookedAt, bookingType, fareAmount, seatAmount, bagAmount, productAmount,
            confirmedTotalAmount, issuedTickets);
    }

    /// <summary>
    /// Calls the Offer MS reprice endpoint for every flight offer in the basket.
    /// Reprice returns the latest dynamic fares and tax lines, and sets Validated = true.
    /// Throws if any offer is not found / expired, or if validation still fails after reprice.
    /// Returns a map of StoredOfferId → repriced offer data for use in order enrichment.
    /// </summary>
    private async Task<Dictionary<Guid, RepriceOfferDto>> RepriceAndValidateOffersAsync(
        string? basketDataJson, CancellationToken cancellationToken)
    {
        var offerRefs = ParseOfferRefsFromBasketData(basketDataJson);
        var result = new Dictionary<Guid, RepriceOfferDto>();

        foreach (var (offerId, sessionId) in offerRefs)
        {
            var repriced = await _offerServiceClient.RepriceOfferAsync(offerId, sessionId, cancellationToken);
            if (repriced is null)
                throw new InvalidOperationException($"Offer {offerId} could not be found or has expired. Customer must re-search.");
            if (!repriced.Validated)
                throw new InvalidOperationException($"Offer {offerId} needs to be priced before it can be confirmed.");
            result[offerId] = repriced;
        }

        return result;
    }

    /// <summary>
    /// Builds the enriched offers payload to pass to the Order MS confirm endpoint.
    /// Each entry matches offerId + cabinCode from the basket to a repriced offer item.
    /// </summary>
    private static List<object> BuildEnrichedOffersPayload(
        string? basketDataJson,
        Dictionary<Guid, RepriceOfferDto> repricedOffers)
    {
        var payload = new List<object>();
        if (basketDataJson is null) return payload;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("flightOffers", out var offersEl) ||
                offersEl.ValueKind != JsonValueKind.Array)
                return payload;

            foreach (var offer in offersEl.EnumerateArray())
            {
                if (!offer.TryGetProperty("offerId", out var offerIdEl) ||
                    !offerIdEl.TryGetGuid(out var offerId))
                    continue;

                var cabinCode = offer.TryGetProperty("cabinCode", out var cc) ? cc.GetString() ?? "" : "";

                if (!repricedOffers.TryGetValue(offerId, out var repriced)) continue;

                // Find the specific cabin item from the repriced offer
                var item = repriced.Offers.FirstOrDefault(o =>
                    string.Equals(o.CabinCode, cabinCode, StringComparison.OrdinalIgnoreCase))
                    ?? repriced.Offers.FirstOrDefault();

                if (item is null) continue;

                // The Offer MS returns per-pax prices; multiply by the passenger count
                // stored on the basket offer so the Order MS receives all-pax totals.
                var passengerCount = 1;
                if (offer.TryGetProperty("passengerCount", out var pcEl) &&
                    pcEl.ValueKind == JsonValueKind.Number &&
                    pcEl.TryGetInt32(out var pc) && pc > 0)
                    passengerCount = pc;

                // Use basket-locked amounts — prices are fixed at search time (stored offer pattern).
                // Repriced item amounts reflect current occupancy and must not override the locked price.
                var lockedBaseFare = offer.TryGetProperty("baseFareAmount", out var bfa) ? bfa.GetDecimal() : item.BaseFareAmount * passengerCount;
                var lockedTax      = offer.TryGetProperty("taxAmount",      out var ta)  ? ta.GetDecimal()  : item.TaxAmount      * passengerCount;
                var lockedTotal    = offer.TryGetProperty("totalAmount",    out var tot) ? tot.GetDecimal() : item.TotalAmount    * passengerCount;

                payload.Add(new
                {
                    offerId        = offerId,
                    cabinCode      = item.CabinCode,
                    baseFareAmount = lockedBaseFare,
                    taxAmount      = lockedTax,
                    totalAmount    = lockedTotal,
                    passengerCount = passengerCount,
                    taxLines       = item.TaxLines?.Select(tl => new
                    {
                        code        = tl.Code,
                        amount      = tl.Amount * passengerCount,
                        description = tl.Description
                    })
                });
            }
        }
        catch { /* Return whatever was built */ }

        return payload;
    }

    // ── Response builders ─────────────────────────────────────────────────────

    private static (string CardType, string CardLast4, string? MaskedCardNumber) ExtractCardInfo(
        string? cardNumber, string? cardholderName)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return ("Card", "0000", null);

        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits.PadLeft(4, '0');
        var masked = digits.Length >= 10
            ? digits[..6] + new string('X', digits.Length - 10) + digits[^4..]
            : null;

        var cardType = digits.FirstOrDefault() switch
        {
            '4' => "Visa",
            '5' => "Mastercard",
            '3' => "Amex",
            '6' => "Discover",
            _ => "Card"
        };

        return (cardType, last4, masked);
    }

    private static OrderResponse BuildOrderResponse(
        OrderMsConfirmOrderResult confirmedOrder,
        string? basketDataJson,
        ConfirmBasketCommand command,
        string paymentId,
        (string CardType, string CardLast4, string? MaskedCardNumber) cardInfo,
        string bookedAt,
        string bookingType,
        decimal fareAmount,
        decimal seatAmount,
        decimal bagAmount,
        decimal productAmount,
        decimal totalAmount,
        List<IssuedTicket> issuedTickets)
    {
        var (segments, passengerRefs) = ParseSegmentsAndPassengerRefs(basketDataJson);
        var passengers = ParseFullPassengers(basketDataJson);

        // Build e-ticket lookup: segmentId (basketItemId) → list of (passengerId, eTicketNumber)
        var eTicketsBySegment = BuildETicketLookup(issuedTickets, segments);

        var orderItems = new List<ConfirmedOrderItem>();
        var idx = 1;
        foreach (var seg in segments)
        {
            var matchedItem = confirmedOrder.OrderItems.FirstOrDefault(i =>
                string.Equals(i.FlightNumber, seg.FlightNumber, StringComparison.OrdinalIgnoreCase));

            var paxRefs = passengerRefs;
            var eTickets = eTicketsBySegment.TryGetValue(seg.SegmentId, out var tickets)
                ? tickets
                : [];

            orderItems.Add(new ConfirmedOrderItem
            {
                OrderItemId    = $"OI-{idx++}",
                Type           = "Flight",
                SegmentRef     = seg.SegmentId,
                PassengerRefs  = paxRefs,
                FareFamily     = matchedItem?.FareFamily,
                FareBasisCode  = matchedItem?.FareBasisCode,
                UnitPrice      = matchedItem != null && paxRefs.Count > 0
                    ? Math.Round(matchedItem.TotalAmount / Math.Max(paxRefs.Count, 1), 2, MidpointRounding.AwayFromZero)
                    : matchedItem?.TotalAmount ?? 0m,
                Taxes          = matchedItem?.TaxAmount ?? 0m,
                TotalPrice     = matchedItem?.TotalAmount ?? 0m,
                IsRefundable   = matchedItem != null ? ParseBoolFromItem(matchedItem, "isRefundable") : null,
                IsChangeable   = matchedItem != null ? ParseBoolFromItem(matchedItem, "isChangeable") : null,
                PaymentReference = paymentId,
                ETickets       = eTickets.Count > 0 ? eTickets : null
            });
        }

        AppendAncillaryItems(basketDataJson, orderItems, segments, paymentId, ref idx);

        var isReward = string.Equals(bookingType, "Reward", StringComparison.OrdinalIgnoreCase);
        var loyaltyNumber = basketDataJson != null ? ParseLoyaltyNumber(basketDataJson) : null;

        ConfirmedPointsRedemption? pointsRedemption = null;
        if (isReward && !string.IsNullOrEmpty(loyaltyNumber) && command.LoyaltyPointsToRedeem.HasValue)
        {
            pointsRedemption = new ConfirmedPointsRedemption
            {
                RedemptionReference = Guid.NewGuid().ToString(),
                LoyaltyNumber       = loyaltyNumber,
                PointsRedeemed      = command.LoyaltyPointsToRedeem.Value,
                Status              = "Settled",
                AuthorisedAt        = bookedAt,
                SettledAt           = bookedAt
            };
        }

        var paymentDescription = isReward ? "Taxes and fees" : "Full payment";
        var payment = new ConfirmedPayment
        {
            PaymentReference  = paymentId,
            Description       = paymentDescription,
            Method            = command.PaymentMethod,
            CardLast4         = cardInfo.CardLast4,
            CardType          = cardInfo.CardType,
            CardholderName    = command.CardholderName,
            MaskedCardNumber  = cardInfo.MaskedCardNumber,
            AuthorisedAmount  = totalAmount,
            SettledAmount     = totalAmount,
            Currency          = confirmedOrder.CurrencyCode,
            Status            = "Settled",
            AuthorisedAt      = bookedAt,
            SettledAt         = bookedAt
        };

        return new OrderResponse
        {
            OrderId          = confirmedOrder.OrderId,
            BookingReference = confirmedOrder.BookingReference,
            OrderStatus      = confirmedOrder.OrderStatus,
            BookingType      = bookingType,
            ChannelCode      = command.ChannelCode,
            Currency         = confirmedOrder.CurrencyCode,
            BookedAt         = bookedAt,
            FareTotal        = fareAmount,
            SeatTotal        = seatAmount,
            BagTotal         = bagAmount,
            ProductTotal     = productAmount,
            TotalAmount      = totalAmount,
            TotalPointsAmount = isReward && command.LoyaltyPointsToRedeem.HasValue
                ? command.LoyaltyPointsToRedeem.Value
                : null,
            Passengers       = passengers,
            FlightSegments   = segments,
            OrderItems       = orderItems,
            Payment          = payment,
            PointsRedemption = pointsRedemption
        };
    }

    private static (List<ConfirmedFlightSegment> segments, List<string> passengerRefs)
        ParseSegmentsAndPassengerRefs(string? basketDataJson)
    {
        var segments = new List<ConfirmedFlightSegment>();
        var passengerRefs = new List<string>();

        if (basketDataJson is null) return (segments, passengerRefs);

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("passengers", out var paxEl) && paxEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in paxEl.EnumerateArray())
                {
                    if (p.TryGetProperty("passengerId", out var pid))
                        passengerRefs.Add(pid.GetString() ?? "");
                }
            }

            if (!root.TryGetProperty("flightOffers", out var offersEl) ||
                offersEl.ValueKind != JsonValueKind.Array)
                return (segments, passengerRefs);

            var idx = 1;
            foreach (var offer in offersEl.EnumerateArray())
            {
                var basketItemId = offer.TryGetProperty("basketItemId", out var bid)
                    ? bid.GetString() ?? $"SEG-{idx}"
                    : $"SEG-{idx}";

                var depDate = offer.TryGetProperty("departureDate", out var dd) ? dd.GetString() ?? "" : "";
                var depTime = offer.TryGetProperty("departureTime", out var dt) ? dt.GetString() ?? "00:00" : "00:00";
                var arrTime = offer.TryGetProperty("arrivalTime",   out var at) ? at.GetString() ?? "00:00" : "00:00";
                var flightNumber = offer.TryGetProperty("flightNumber", out var fn) ? fn.GetString() ?? "" : "";
                var carrier = flightNumber.Length >= 2 ? new string(flightNumber.TakeWhile(char.IsLetter).ToArray()) : "AX";

                segments.Add(new ConfirmedFlightSegment
                {
                    SegmentId         = basketItemId,
                    FlightNumber      = flightNumber,
                    Origin            = offer.TryGetProperty("origin",       out var o) ? o.GetString() ?? "" : "",
                    Destination       = offer.TryGetProperty("destination",  out var d) ? d.GetString() ?? "" : "",
                    DepartureDateTime = string.IsNullOrEmpty(depDate) ? "" : $"{depDate}T{depTime}:00Z",
                    ArrivalDateTime   = string.IsNullOrEmpty(depDate) ? "" : $"{depDate}T{arrTime}:00Z",
                    AircraftType      = offer.TryGetProperty("aircraftType", out var ac) ? ac.GetString() ?? "" : "",
                    OperatingCarrier  = carrier,
                    MarketingCarrier  = carrier,
                    CabinCode         = offer.TryGetProperty("cabinCode",    out var cc) ? cc.GetString() ?? "" : "",
                    BookingClass      = offer.TryGetProperty("fareBasisCode", out var fb)
                        ? (fb.GetString() ?? "Y").Substring(0, 1)
                        : "Y"
                });
                idx++;
            }
        }
        catch { /* Return whatever was parsed */ }

        return (segments, passengerRefs);
    }

    private static List<ConfirmedPassenger> ParseFullPassengers(string? basketDataJson)
    {
        var passengers = new List<ConfirmedPassenger>();
        if (basketDataJson is null) return passengers;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("passengers", out var paxEl) || paxEl.ValueKind != JsonValueKind.Array)
                return passengers;

            foreach (var p in paxEl.EnumerateArray())
            {
                ConfirmedPassengerContacts? contacts = null;
                if (p.TryGetProperty("contacts", out var cEl) && cEl.ValueKind == JsonValueKind.Object)
                {
                    contacts = new ConfirmedPassengerContacts
                    {
                        Email = cEl.TryGetProperty("email", out var em) ? em.GetString() : null,
                        Phone = cEl.TryGetProperty("phone", out var ph) ? ph.GetString() : null
                    };
                }

                var docs = new List<ConfirmedTravelDoc>();
                if (p.TryGetProperty("docs", out var docsEl) && docsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var doc2 in docsEl.EnumerateArray())
                    {
                        docs.Add(new ConfirmedTravelDoc
                        {
                            Type           = doc2.TryGetProperty("type",           out var t)  ? t.GetString()  ?? "" : "",
                            Number         = doc2.TryGetProperty("number",         out var n)  ? n.GetString()  ?? "" : "",
                            IssuingCountry = doc2.TryGetProperty("issuingCountry", out var ic) ? ic.GetString() ?? "" : "",
                            ExpiryDate     = doc2.TryGetProperty("expiryDate",     out var ed) ? ed.GetString() ?? "" : "",
                            Nationality    = doc2.TryGetProperty("nationality",    out var na) ? na.GetString() ?? "" : ""
                        });
                    }
                }

                passengers.Add(new ConfirmedPassenger
                {
                    PassengerId   = p.TryGetProperty("passengerId",   out var pid) ? pid.GetString() ?? "" : "",
                    Type          = p.TryGetProperty("type",          out var tp)  ? tp.GetString()  ?? "ADT" : "ADT",
                    GivenName     = p.TryGetProperty("givenName",     out var gn)  ? gn.GetString()  ?? "" : "",
                    Surname       = p.TryGetProperty("surname",       out var sn)  ? sn.GetString()  ?? "" : "",
                    Dob           = p.TryGetProperty("dob",           out var dob) ? dob.GetString() : null,
                    Gender        = p.TryGetProperty("gender",        out var gen) && gen.ValueKind != JsonValueKind.Null
                        ? gen.GetString() : null,
                    LoyaltyNumber = p.TryGetProperty("loyaltyNumber", out var ln) && ln.ValueKind != JsonValueKind.Null
                        ? ln.GetString() : null,
                    Contacts      = contacts,
                    Docs          = docs
                });
            }
        }
        catch { /* Return whatever was parsed */ }

        return passengers;
    }

    private static Dictionary<string, List<ConfirmedETicket>> BuildETicketLookup(
        List<IssuedTicket> issuedTickets,
        List<ConfirmedFlightSegment> segments)
    {
        var lookup = new Dictionary<string, List<ConfirmedETicket>>(StringComparer.OrdinalIgnoreCase);

        foreach (var ticket in issuedTickets)
        {
            foreach (var segRef in ticket.SegmentIds)
            {
                // SegmentIds on tickets are basketItemIds — match them directly to SegmentId
                var segmentId = segments.FirstOrDefault(s =>
                    string.Equals(s.SegmentId, segRef, StringComparison.OrdinalIgnoreCase))?.SegmentId
                    ?? segRef;

                if (!lookup.ContainsKey(segmentId))
                    lookup[segmentId] = [];
                lookup[segmentId].Add(new ConfirmedETicket
                {
                    PassengerId    = ticket.PassengerId,
                    ETicketNumber  = ticket.ETicketNumber
                });
            }
        }

        return lookup;
    }

    private static void AppendAncillaryItems(
        string? basketDataJson,
        List<ConfirmedOrderItem> orderItems,
        List<ConfirmedFlightSegment> segments,
        string paymentId,
        ref int idx)
    {
        if (basketDataJson is null) return;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            // Seats — segmentId on the basket seat is the inventoryId; resolve to basketItemId
            if (root.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatsEl.EnumerateArray())
                {
                    var inventoryId = seat.TryGetProperty("segmentId",    out var sid) ? sid.GetString() ?? "" : "";
                    var paxId       = seat.TryGetProperty("passengerId",  out var pid) ? pid.GetString() ?? "" : "";
                    var price       = seat.TryGetProperty("price",        out var p)   ? p.GetDecimal() : 0m;
                    var tax         = seat.TryGetProperty("tax",          out var t)   ? t.GetDecimal() : 0m;
                    var seatNumber  = DecodeSeatNumber(seat.TryGetProperty("seatOfferId", out var soi) ? soi.GetString() : null);
                    var seatPos     = seat.TryGetProperty("seatPosition", out var sp)  ? sp.GetString() : null;

                    // Map inventoryId → basketItemId (SegmentId) for consistent cross-referencing
                    var segRef = segments.FirstOrDefault(s =>
                        root.TryGetProperty("flightOffers", out var offs) &&
                        offs.ValueKind == JsonValueKind.Array &&
                        offs.EnumerateArray().Any(o =>
                            o.TryGetProperty("inventoryId", out var inv) &&
                            string.Equals(inv.GetString(), inventoryId, StringComparison.OrdinalIgnoreCase) &&
                            o.TryGetProperty("basketItemId", out var bk) &&
                            string.Equals(bk.GetString(), s.SegmentId, StringComparison.OrdinalIgnoreCase)))
                        ?.SegmentId ?? inventoryId;

                    orderItems.Add(new ConfirmedOrderItem
                    {
                        OrderItemId    = $"OI-{idx++}",
                        Type           = "Seat",
                        SegmentRef     = segRef,
                        PassengerRefs  = string.IsNullOrEmpty(paxId) ? [] : [paxId],
                        UnitPrice      = price,
                        Taxes          = tax,
                        TotalPrice     = price,
                        PaymentReference = paymentId,
                        SeatNumber     = seatNumber,
                        SeatPosition   = seatPos
                    });
                }
            }

            // Bags
            if (root.TryGetProperty("bags", out var bagsEl) && bagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var bag in bagsEl.EnumerateArray())
                {
                    var inventoryId    = bag.TryGetProperty("segmentId",      out var sid) ? sid.GetString() ?? "" : "";
                    var paxId          = bag.TryGetProperty("passengerId",    out var pid) ? pid.GetString() ?? "" : "";
                    var price          = bag.TryGetProperty("price",          out var p)   ? p.GetDecimal() : 0m;
                    var tax            = bag.TryGetProperty("tax",            out var t)   ? t.GetDecimal() : 0m;
                    var additionalBags = bag.TryGetProperty("additionalBags", out var ab)  ? ab.GetInt32()  : 1;

                    var segRef = ResolveSegmentRef(root, inventoryId, segments);

                    orderItems.Add(new ConfirmedOrderItem
                    {
                        OrderItemId    = $"OI-{idx++}",
                        Type           = "Bag",
                        SegmentRef     = segRef,
                        PassengerRefs  = string.IsNullOrEmpty(paxId) ? [] : [paxId],
                        UnitPrice      = price,
                        Taxes          = tax,
                        TotalPrice     = price,
                        PaymentReference = paymentId,
                        AdditionalBags = additionalBags
                    });
                }
            }

            // Products
            if (root.TryGetProperty("products", out var productsEl) && productsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var product in productsEl.EnumerateArray())
                {
                    var paxId   = product.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "";
                    var segRef2 = product.TryGetProperty("segmentRef",  out var sr)  ? sr.GetString()  ?? "" : "";
                    var price   = product.TryGetProperty("price",       out var p)   ? p.GetDecimal() : 0m;
                    var tax     = product.TryGetProperty("tax",         out var t)   ? t.GetDecimal() : 0m;
                    var name    = product.TryGetProperty("name",        out var n)   ? n.GetString() : null;
                    var offerId = product.TryGetProperty("offerId",     out var oid) ? oid.GetString() : null;

                    // segmentRef on products is a basketItemId — map to SegmentId
                    var resolvedSegRef = segments.FirstOrDefault(s =>
                        string.Equals(s.SegmentId, segRef2, StringComparison.OrdinalIgnoreCase))?.SegmentId
                        ?? segRef2;

                    orderItems.Add(new ConfirmedOrderItem
                    {
                        OrderItemId    = $"OI-{idx++}",
                        Type           = "Product",
                        SegmentRef     = resolvedSegRef,
                        PassengerRefs  = string.IsNullOrEmpty(paxId) ? [] : [paxId],
                        UnitPrice      = price,
                        Taxes          = tax,
                        TotalPrice     = price,
                        PaymentReference = paymentId,
                        ProductName    = name,
                        ProductOfferId = offerId
                    });
                }
            }

            // SSRs (no charge)
            if (root.TryGetProperty("ssrSelections", out var ssrsEl) && ssrsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ssr in ssrsEl.EnumerateArray())
                {
                    var ssrCode = ssr.TryGetProperty("ssrCode",      out var sc) ? sc.GetString() ?? "" : "";
                    var paxRef  = ssr.TryGetProperty("passengerRef", out var pr) ? pr.GetString() ?? "" : "";
                    var segRef3 = ssr.TryGetProperty("segmentRef",   out var sr) ? sr.GetString() ?? "" : "";

                    var resolvedSegRef = segments.FirstOrDefault(s =>
                        string.Equals(s.SegmentId, segRef3, StringComparison.OrdinalIgnoreCase))?.SegmentId
                        ?? segRef3;

                    orderItems.Add(new ConfirmedOrderItem
                    {
                        OrderItemId    = $"OI-{idx++}",
                        Type           = "SSR",
                        SegmentRef     = resolvedSegRef,
                        PassengerRefs  = string.IsNullOrEmpty(paxRef) ? [] : [paxRef],
                        UnitPrice      = 0m,
                        Taxes          = 0m,
                        TotalPrice     = 0m,
                        PaymentReference = string.Empty,
                        SsrCode        = ssrCode
                    });
                }
            }
        }
        catch { /* Return whatever was appended */ }
    }

    private static string ResolveSegmentRef(
        JsonElement root,
        string inventoryId,
        List<ConfirmedFlightSegment> segments)
    {
        if (!root.TryGetProperty("flightOffers", out var offersEl) ||
            offersEl.ValueKind != JsonValueKind.Array)
            return inventoryId;

        foreach (var offer in offersEl.EnumerateArray())
        {
            if (!offer.TryGetProperty("inventoryId", out var inv)) continue;
            if (!string.Equals(inv.GetString(), inventoryId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!offer.TryGetProperty("basketItemId", out var bk)) continue;
            var basketItemId = bk.GetString() ?? "";
            var matched = segments.FirstOrDefault(s =>
                string.Equals(s.SegmentId, basketItemId, StringComparison.OrdinalIgnoreCase));
            if (matched is not null) return matched.SegmentId;
        }

        return inventoryId;
    }

    private static bool? ParseBoolFromItem(ConfirmedOrderItemResult item, string propertyName)
    {
        // ConfirmedOrderItemResult doesn't currently expose isRefundable/isChangeable;
        // return null (unknown) until the Order MS surfaces these.
        return null;
    }

    private static decimal CalculateTotalFromBasket(string? basketDataJson)
    {
        decimal flightTotal = 0m;
        if (basketDataJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(basketDataJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("flightOffers", out var offersEl) && offersEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var offer in offersEl.EnumerateArray())
                    {
                        if (offer.TryGetProperty("totalAmount", out var tot) && tot.ValueKind == JsonValueKind.Number)
                            flightTotal += tot.GetDecimal();
                    }
                }
            }
            catch { }
        }

        return flightTotal + ParseSeatAmountFromBasketData(basketDataJson) + ParseBagAmountFromBasketData(basketDataJson) + ParseProductAmountFromBasketData(basketDataJson);
    }

    private static decimal ParseSeatAmountFromBasketData(string? basketDataJson)
    {
        if (basketDataJson is null) return 0m;
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("seats", out var seatsEl) || seatsEl.ValueKind != JsonValueKind.Array) return 0m;
            decimal total = 0m;
            foreach (var seat in seatsEl.EnumerateArray())
                total += seat.TryGetProperty("price", out var p) ? p.GetDecimal() : 0m;
            return total;
        }
        catch { return 0m; }
    }

    private static decimal ParseBagAmountFromBasketData(string? basketDataJson)
    {
        if (basketDataJson is null) return 0m;
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("bags", out var bagsEl) || bagsEl.ValueKind != JsonValueKind.Array) return 0m;
            decimal total = 0m;
            foreach (var bag in bagsEl.EnumerateArray())
                total += bag.TryGetProperty("price", out var p) ? p.GetDecimal() : 0m;
            return total;
        }
        catch { return 0m; }
    }

    private static decimal ParseProductAmountFromBasketData(string? basketDataJson)
    {
        if (basketDataJson is null) return 0m;
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("products", out var productsEl) || productsEl.ValueKind != JsonValueKind.Array)
                return 0m;
            decimal total = 0m;
            foreach (var product in productsEl.EnumerateArray())
            {
                var price = product.TryGetProperty("price", out var p) ? p.GetDecimal() : 0m;
                total += price;
            }
            return total;
        }
        catch { return 0m; }
    }

    private static List<(Guid OfferId, Guid? SessionId)> ParseOfferRefsFromBasketData(string? basketDataJson)
    {
        var refs = new List<(Guid, Guid?)>();
        if (basketDataJson == null) return refs;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("flightOffers", out var offersEl) &&
                offersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var offer in offersEl.EnumerateArray())
                {
                    if (offer.TryGetProperty("offerId", out var offerIdEl) &&
                        offerIdEl.TryGetGuid(out var offerId))
                    {
                        Guid? sessionId = null;
                        if (offer.TryGetProperty("sessionId", out var sessionIdEl) &&
                            sessionIdEl.TryGetGuid(out var sid))
                            sessionId = sid;

                        refs.Add((offerId, sessionId));
                    }
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return refs;
    }

    private async Task RunInventorySellAsync(
        string? basketDataJson, Guid orderId, Guid basketId, bool isStandby, CancellationToken cancellationToken)
    {
        if (basketDataJson == null) return;
        try
        {
            var (inventoryItems, passengerIds, seatsByInventory) = ParseBasketDataForInventorySell(basketDataJson);
            if (inventoryItems.Count == 0 || passengerIds.Count == 0) return;

            // Standby bookings use a priority of 50 (staff/leisure standby band).
            // For Revenue bookings, use default hold type with no priority.
            var holdType = isStandby ? "Standby" : "Revenue";
            short? standbyPriority = isStandby ? (short)50 : null;

            // Hold all inventory segments in parallel
            await Task.WhenAll(inventoryItems.Select(item =>
            {
                var (inventoryId, cabinCode) = item;
                List<(string? SeatNumber, string? PassengerId)> passengers;
                if (seatsByInventory.TryGetValue(inventoryId.ToString(), out var seats) && seats.Count > 0)
                    passengers = seats;
                else
                    passengers = passengerIds.Select(id => ((string?)null, (string?)id)).ToList();

                return _offerServiceClient.HoldInventoryAsync(inventoryId, cabinCode, passengers, orderId, holdType, standbyPriority, cancellationToken);
            }));

            // Standby bookings do not decrement inventory — they queue on the standby list only.
            if (!isStandby)
                await _offerServiceClient.SellInventoryAsync(orderId, inventoryItems, cancellationToken);
        }
        catch (Exception ex)
        {
            // Inventory operation failure is logged but does not roll back the confirmed order —
            // the booking is already committed and the customer paid. Inventory can be
            // reconciled manually if needed.
            System.Console.Error.WriteLine(
                $"[ConfirmBasket] Inventory operation failed for basket {basketId}: {ex.Message}");
        }
    }

    private async Task<List<IssuedTicket>> RunTicketIssuanceAsync(
        string? basketDataJson,
        ConfirmBasketCommand command,
        string paymentId,
        decimal totalAmount,
        string currency,
        OrderMsConfirmOrderResult confirmedOrder,
        Dictionary<Guid, RepriceOfferDto> repricedOffers,
        CancellationToken cancellationToken)
    {
        if (basketDataJson == null) return [];
        try
        {
            var (passengers, segments) = ParseBasketDataForTickets(basketDataJson);
            if (passengers.Count == 0 || segments.Count == 0) return [];

            var fareConstruction = BuildFareConstruction(basketDataJson, repricedOffers);
            // FOP amount on the e-ticket covers the air fare only (base + taxes per pax),
            // not the entire order total which also includes seats and ancillaries.
            var perPaxAmount = fareConstruction != null
                ? Math.Round(fareConstruction.BaseFare + fareConstruction.TotalTaxes, 2, MidpointRounding.AwayFromZero)
                : (passengers.Count > 0 ? Math.Round(totalAmount / passengers.Count, 2, MidpointRounding.AwayFromZero) : totalAmount);
            var formOfPayment = BuildFormOfPayment(command, paymentId, perPaxAmount, currency);
            var passengersWithPayment = passengers
                .Select(p => new TicketPassenger
                {
                    PassengerId = p.PassengerId,
                    GivenName = p.GivenName,
                    Surname = p.Surname,
                    Dob = p.Dob,
                    FareConstruction = fareConstruction,
                    FormOfPayment = formOfPayment
                })
                .ToList();

            var issuedTickets = await _deliveryServiceClient.IssueTicketsAsync(
                command.BasketId,
                confirmedOrder.BookingReference,
                passengersWithPayment,
                segments,
                cancellationToken);

            if (issuedTickets.Count > 0)
            {
                var eTicketsJson = JsonSerializer.Serialize(
                    issuedTickets.Select(t => new { t.PassengerId, t.SegmentIds, t.ETicketNumber }),
                    SharedJsonOptions.CamelCase);
                await _orderServiceClient.UpdateOrderETicketsAsync(
                    confirmedOrder.BookingReference, eTicketsJson, cancellationToken);
            }

            return issuedTickets;
        }
        catch (Exception ex)
        {
            // Ticket issuance failure after order confirmation — order is confirmed, tickets need manual issuance
            System.Console.Error.WriteLine($"[ConfirmBasket] Ticket issuance failed for {confirmedOrder.BookingReference}: {ex.Message}");
            return [];
        }
    }

    private async Task RunCustomerLinkAsync(
        string? basketDataJson, OrderMsConfirmOrderResult confirmedOrder, CancellationToken cancellationToken)
    {
        try
        {
            var loyaltyNumber = basketDataJson != null ? ParseLoyaltyNumber(basketDataJson) : null;
            if (!string.IsNullOrWhiteSpace(loyaltyNumber))
            {
                await _customerServiceClient.LinkOrderToCustomerAsync(
                    loyaltyNumber, confirmedOrder.OrderId, confirmedOrder.BookingReference,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine(
                $"[ConfirmBasket] Customer order link failed for {confirmedOrder.BookingReference}: {ex.Message}");
        }
    }

    private static TicketFormOfPayment BuildFormOfPayment(
        ConfirmBasketCommand command, string paymentId, decimal amount, string currency)
    {
        string? maskedPan = null;
        string? cardType = null;
        string? expiryMmYy = null;

        if (!string.IsNullOrWhiteSpace(command.CardNumber))
        {
            var digits = new string(command.CardNumber.Where(char.IsDigit).ToArray());
            if (digits.Length >= 10)
            {
                maskedPan = digits[..6] + new string('X', digits.Length - 10) + digits[^4..];
            }

            cardType = digits.FirstOrDefault() switch
            {
                '4' => "VI",
                '5' => "MC",
                '3' => "AX",
                '6' => "DC",
                _ => null
            };
        }

        if (!string.IsNullOrWhiteSpace(command.ExpiryDate))
        {
            var parts = command.ExpiryDate.Split('/');
            if (parts.Length == 2)
            {
                var month = parts[0].PadLeft(2, '0');
                var year = parts[1].Length == 4 ? parts[1][2..] : parts[1].PadLeft(2, '0');
                expiryMmYy = month + year;
            }
            else if (command.ExpiryDate.Length == 4 && command.ExpiryDate.All(char.IsDigit))
            {
                expiryMmYy = command.ExpiryDate;
            }
        }

        var type = command.PaymentMethod.ToUpperInvariant() switch
        {
            "CREDITCARD" or "CC" => "CC",
            "DEBITCARD" or "DC" => "DC",
            "CASH" => "CASH",
            _ => command.PaymentMethod.ToUpperInvariant()
        };

        return new TicketFormOfPayment
        {
            Type = type,
            CardType = cardType,
            MaskedPan = maskedPan,
            ExpiryMmYy = expiryMmYy,
            ApprovalCode = paymentId,
            Amount = amount,
            Currency = currency
        };
    }

    private static (List<TicketPassenger> passengers, List<TicketSegment> segments) ParseBasketDataForTickets(
        string basketDataJson)
    {
        var passengers = new List<TicketPassenger>();
        var segments = new List<TicketSegment>();

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("passengers", out var passengersEl) &&
                passengersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in passengersEl.EnumerateArray())
                {
                    passengers.Add(new TicketPassenger
                    {
                        PassengerId = p.TryGetProperty("passengerId", out var v) ? v.GetString() ?? string.Empty : string.Empty,
                        GivenName = p.TryGetProperty("givenName", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Surname = p.TryGetProperty("surname", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Dob = p.TryGetProperty("dob", out v) ? v.GetString() : null
                    });
                }
            }

            // Build SSR lookup keyed by inventoryId (the segmentRef stored on each SSR selection).
            var ssrsByInventoryId = new Dictionary<string, List<SegmentSsrCode>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("ssrSelections", out var ssrsEl) && ssrsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ssr in ssrsEl.EnumerateArray())
                {
                    var segRef  = ssr.TryGetProperty("segmentRef",   out var sr) ? sr.GetString() ?? "" : "";
                    var paxRef  = ssr.TryGetProperty("passengerRef", out var pr) ? pr.GetString() ?? "" : "";
                    var code    = ssr.TryGetProperty("ssrCode",      out var sc) ? sc.GetString() ?? "" : "";
                    var desc    = ssr.TryGetProperty("description",  out var dv) ? dv.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(segRef) || string.IsNullOrEmpty(code)) continue;
                    if (!ssrsByInventoryId.ContainsKey(segRef))
                        ssrsByInventoryId[segRef] = [];
                    ssrsByInventoryId[segRef].Add(new SegmentSsrCode
                    {
                        PassengerId = paxRef,
                        Code        = code,
                        Description = desc,
                        SegmentRef  = segRef
                    });
                }
            }

            // Build a lookup of seat assignments keyed by inventoryId (which is how the
            // web app stores the segmentId on each seat selection in the basket).
            var seatsByInventoryId = new Dictionary<string, List<SeatAssignmentItem>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatsEl.EnumerateArray())
                {
                    var paxId   = seat.TryGetProperty("passengerId",  out var spid) ? spid.GetString() ?? "" : "";
                    var segId   = seat.TryGetProperty("segmentId",    out var ssid) ? ssid.GetString() ?? "" : "";
                    var seatNum = DecodeSeatNumber(seat.TryGetProperty("seatOfferId", out var soi) ? soi.GetString() : null) ?? "";
                    if (!string.IsNullOrEmpty(paxId) && !string.IsNullOrEmpty(segId) && !string.IsNullOrEmpty(seatNum))
                    {
                        if (!seatsByInventoryId.ContainsKey(segId))
                            seatsByInventoryId[segId] = [];
                        seatsByInventoryId[segId].Add(new SeatAssignmentItem { PassengerId = paxId, SeatNumber = seatNum });
                    }
                }
            }

            if (root.TryGetProperty("flightOffers", out var offersEl) &&
                offersEl.ValueKind == JsonValueKind.Array)
            {
                var idx = 1;
                foreach (var offer in offersEl.EnumerateArray())
                {
                    var segmentId = offer.TryGetProperty("basketItemId", out var bid)
                        ? bid.GetString() ?? $"SEG-{idx}"
                        : $"SEG-{idx}";

                    var inventoryId = offer.TryGetProperty("inventoryId", out var v) ? v.GetString() ?? string.Empty : string.Empty;

                    segments.Add(new TicketSegment
                    {
                        SegmentId = segmentId,
                        InventoryId = inventoryId,
                        FlightNumber = offer.TryGetProperty("flightNumber", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        DepartureDate = offer.TryGetProperty("departureDate", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Origin = offer.TryGetProperty("origin", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        Destination = offer.TryGetProperty("destination", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        CabinCode = offer.TryGetProperty("cabinCode", out v) ? v.GetString() ?? string.Empty : string.Empty,
                        FareBasisCode = offer.TryGetProperty("fareBasisCode", out v) ? v.GetString() : null,
                        SeatAssignments = seatsByInventoryId.TryGetValue(inventoryId, out var segSeats) ? segSeats : [],
                        SsrCodes = ssrsByInventoryId.TryGetValue(inventoryId, out var segSsrs) ? segSsrs : []
                    });
                    idx++;
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return (passengers, segments);
    }

    private static TicketFareConstruction? BuildFareConstruction(
        string basketDataJson,
        Dictionary<Guid, RepriceOfferDto> repricedOffers)
    {
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("flightOffers", out var offersEl) ||
                offersEl.ValueKind != JsonValueKind.Array)
                return null;

            // Collect ordered per-segment fare components and aggregate taxes.
            // The Offer MS reprice returns per-pax amounts; these are used directly as the
            // per-passenger NUC fare components for the IATA fare calculation line.
            var components = new List<(string Origin, string Carrier, string Destination, decimal NucAmount)>();
            var taxAccumulator = new Dictionary<string, (decimal Total, string Currency, string? Description)>(StringComparer.OrdinalIgnoreCase);
            string? collectingCurrency = null;

            foreach (var offer in offersEl.EnumerateArray())
            {
                if (!offer.TryGetProperty("offerId", out var offerIdEl) ||
                    !offerIdEl.TryGetGuid(out var offerId))
                    continue;

                if (!repricedOffers.TryGetValue(offerId, out var repriced)) continue;

                var origin      = offer.TryGetProperty("origin",       out var o)  ? o.GetString()  ?? "" : "";
                var destination = offer.TryGetProperty("destination",  out var d)  ? d.GetString()  ?? "" : "";
                var flightNum   = offer.TryGetProperty("flightNumber", out var fn) ? fn.GetString() ?? "" : "";
                var cabinCode   = offer.TryGetProperty("cabinCode",    out var cc) ? cc.GetString() ?? "" : "";
                var carrier     = ExtractCarrierCode(flightNum);

                if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination) || carrier.Length < 2)
                    continue;

                var item = repriced.Offers.FirstOrDefault(i =>
                    string.Equals(i.CabinCode, cabinCode, StringComparison.OrdinalIgnoreCase))
                    ?? repriced.Offers.FirstOrDefault();

                if (item is null) continue;

                collectingCurrency ??= item.CurrencyCode;

                // The Offer MS returns per-pax amounts; use directly as the NUC component.
                var perPaxComponent = Math.Round(item.BaseFareAmount, 2, MidpointRounding.AwayFromZero);
                components.Add((origin.ToUpperInvariant(), carrier.ToUpperInvariant(), destination.ToUpperInvariant(), perPaxComponent));

                if (item.TaxLines != null)
                {
                    foreach (var tl in item.TaxLines)
                    {
                        if (taxAccumulator.TryGetValue(tl.Code, out var existing))
                            taxAccumulator[tl.Code] = (existing.Total + tl.Amount, existing.Currency, existing.Description);
                        else
                            taxAccumulator[tl.Code] = (tl.Amount, item.CurrencyCode, tl.Description);
                    }
                }
            }

            if (components.Count == 0 || string.IsNullOrEmpty(collectingCurrency)) return null;

            // NUC total = sum of per-pax component amounts across all segments.
            var nucTotal = components.Sum(c => c.NucAmount);

            // Per-pax tax lines from the Offer MS reprice — already per-pax amounts.
            var taxLines = taxAccumulator.Select(kv => new TicketTaxLine
            {
                Code        = kv.Key,
                Amount      = Math.Round(kv.Value.Total, 2, MidpointRounding.AwayFromZero),
                Currency    = kv.Value.Currency,
                Description = kv.Value.Description
            }).ToList();
            var totalTaxes = taxLines.Sum(t => t.Amount);

            // Build IATA linear fare calculation string: "LON BA JFK 300.00 BA LON 300.00 NUC600.00 END ROE1.000000"
            var sb = new StringBuilder(components[0].Origin);
            foreach (var (_, carrier2, dest, nuc) in components)
                sb.Append($" {carrier2} {dest} {nuc:F2}");
            sb.Append($" NUC{nucTotal:F2} END ROE1.000000");

            return new TicketFareConstruction
            {
                PricingCurrency    = collectingCurrency,
                CollectingCurrency = collectingCurrency,
                BaseFare           = nucTotal, // ROE = 1.0, so NUC amount equals local fare
                EquivalentFarePaid = nucTotal,
                NucAmount          = nucTotal,
                RoeApplied         = 1.0m,
                FareCalculationLine = sb.ToString(),
                Taxes      = taxLines,
                TotalTaxes = totalTaxes
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractCarrierCode(string flightNumber)
    {
        // IATA carrier codes are 2 alpha characters at the start of the flight number (e.g. "BA1234" → "BA").
        var alpha = new string(flightNumber.TakeWhile(char.IsLetter).ToArray());
        return alpha.Length >= 2 ? alpha[..2] : alpha;
    }

    private static string? ParseLoyaltyNumber(string basketDataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            if (doc.RootElement.TryGetProperty("loyaltyNumber", out var el) &&
                el.ValueKind == JsonValueKind.String)
            {
                return el.GetString();
            }
        }
        catch { /* Return null */ }
        return null;
    }

    private static (
        List<(Guid InventoryId, string CabinCode)> items,
        List<string> passengerIds,
        Dictionary<string, List<(string? SeatNumber, string? PassengerId)>> seatsByInventory)
        ParseBasketDataForInventorySell(string basketDataJson)
    {
        var items = new List<(Guid, string)>();
        var passengerIds = new List<string>();
        var seatsByInventory = new Dictionary<string, List<(string? SeatNumber, string? PassengerId)>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("passengers", out var passengersEl) &&
                passengersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in passengersEl.EnumerateArray())
                {
                    var paxId = p.TryGetProperty("passengerId", out var pid) ? pid.GetString() : null;
                    if (!string.IsNullOrEmpty(paxId))
                        passengerIds.Add(paxId);
                }
            }

            // seats[].segmentId is the inventoryId — build per-flight seat lists with passenger IDs.
            if (root.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatsEl.EnumerateArray())
                {
                    var segId   = seat.TryGetProperty("segmentId",   out var sid) ? sid.GetString() : null;
                    var seatNum = DecodeSeatNumber(seat.TryGetProperty("seatOfferId", out var soi) ? soi.GetString() : null);
                    var paxId   = seat.TryGetProperty("passengerId", out var pid) ? pid.GetString() : null;
                    if (!string.IsNullOrEmpty(segId))
                    {
                        if (!seatsByInventory.ContainsKey(segId))
                            seatsByInventory[segId] = [];
                        seatsByInventory[segId].Add((string.IsNullOrEmpty(seatNum) ? null : seatNum, paxId));
                    }
                }
            }

            if (root.TryGetProperty("flightOffers", out var offersEl) &&
                offersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var offer in offersEl.EnumerateArray())
                {
                    if (offer.TryGetProperty("inventoryId", out var invIdEl) &&
                        invIdEl.TryGetGuid(out var inventoryId) &&
                        offer.TryGetProperty("cabinCode", out var cabinEl))
                    {
                        var cabinCode = cabinEl.GetString() ?? "Y";
                        items.Add((inventoryId, cabinCode));
                    }
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return (items, passengerIds, seatsByInventory);
    }

    /// <summary>
    /// Decodes a base64 seat offer ID and returns the seat number.
    /// Format: base64("{flightNumber}-{departureDate}-{cabinCode}-{seatNumber}")
    /// e.g. base64("AX001-2026-04-12-Y-35A") → "35A"
    /// </summary>
    private static string? DecodeSeatNumber(string? seatOfferId)
    {
        if (string.IsNullOrEmpty(seatOfferId)) return null;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(seatOfferId));
            var parts = decoded.Split('-');
            // parts: [0]=flightNumber, [1]=year, [2]=month, [3]=day, [4]=cabinCode, [5]=seatNumber
            return parts.Length == 6 ? parts[5] : null;
        }
        catch { return null; }
    }

    private async Task RunSettleAndAuthAncillariesAsync(
        string paymentId, decimal fareAmount, decimal seatAmount, decimal bagAmount, decimal productAmount,
        string? cardNumber, string? expiryDate, string? cvv, string? cardholderName,
        CancellationToken ct)
    {
        // Settle fare — updates the pre-confirm auth PaymentEvent to Settled; payment → Partial
        await _paymentServiceClient.SettleAsync(paymentId, fareAmount, ct);

        // Auth + settle each ancillary type as sequential pairs on the same Payment record
        if (seatAmount > 0)
        {
            await _paymentServiceClient.AuthoriseAsync(paymentId, "Seat", seatAmount, cardNumber, expiryDate, cvv, cardholderName, ct);
            await _paymentServiceClient.SettleAsync(paymentId, seatAmount, ct);
        }

        if (bagAmount > 0)
        {
            await _paymentServiceClient.AuthoriseAsync(paymentId, "Bag", bagAmount, cardNumber, expiryDate, cvv, cardholderName, ct);
            await _paymentServiceClient.SettleAsync(paymentId, bagAmount, ct);
        }

        if (productAmount > 0)
        {
            await _paymentServiceClient.AuthoriseAsync(paymentId, "Product", productAmount, cardNumber, expiryDate, cvv, cardholderName, ct);
            await _paymentServiceClient.SettleAsync(paymentId, productAmount, ct);
        }
    }

    private async Task RunSeatEmdIssuanceAsync(string? basketDataJson, string bookingReference, string seatPaymentId, CancellationToken ct)
    {
        foreach (var (passengerId, segmentId, amount, currency) in ParseSeatEmdItems(basketDataJson))
        {
            try
            {
                await _deliveryServiceClient.IssueDocumentAsync(bookingReference, "SeatAncillary", passengerId, segmentId, amount, currency, seatPaymentId, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ConfirmBasket] Seat EMD issuance failed for {passengerId}: {ex.Message}");
            }
        }
    }

    private async Task RunBagEmdIssuanceAsync(string? basketDataJson, string bookingReference, string bagPaymentId, CancellationToken ct)
    {
        foreach (var (passengerId, segmentId, amount, currency) in ParseBagEmdItems(basketDataJson))
        {
            try
            {
                await _deliveryServiceClient.IssueDocumentAsync(bookingReference, "BagAncillary", passengerId, segmentId, amount, currency, bagPaymentId, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ConfirmBasket] Bag EMD issuance failed for {passengerId}: {ex.Message}");
            }
        }
    }

    private async Task RunProductEmdIssuanceAsync(string? basketDataJson, string bookingReference, string productPaymentId, CancellationToken ct)
    {
        foreach (var (passengerId, segmentId, amount, currency) in ParseProductEmdItems(basketDataJson))
        {
            try
            {
                await _deliveryServiceClient.IssueDocumentAsync(bookingReference, "ProductAncillary", passengerId, segmentId, amount, currency, productPaymentId, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ConfirmBasket] Product EMD issuance failed for {passengerId}: {ex.Message}");
            }
        }
    }

    private static List<(string PassengerId, string SegmentId, decimal Amount, string Currency)> ParseSeatEmdItems(string? basketDataJson)
    {
        var items = new List<(string, string, decimal, string)>();
        if (basketDataJson == null) return items;
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("seats", out var seatsEl) || seatsEl.ValueKind != JsonValueKind.Array) return items;
            foreach (var seat in seatsEl.EnumerateArray())
            {
                var paxId = seat.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "";
                var segId = seat.TryGetProperty("segmentId",   out var sid) ? sid.GetString() ?? "" : "";
                var price = seat.TryGetProperty("price",       out var p)   ? p.GetDecimal() : 0m;
                var tax   = seat.TryGetProperty("tax",         out var t)   ? t.GetDecimal() : 0m;
                var cur   = seat.TryGetProperty("currency",    out var c)   ? c.GetString() ?? "GBP" : "GBP";
                if (price > 0 && !string.IsNullOrEmpty(paxId) && !string.IsNullOrEmpty(segId))
                    items.Add((paxId, segId, price, cur));
            }
        }
        catch { }
        return items;
    }

    private static List<(string PassengerId, string SegmentId, decimal Amount, string Currency)> ParseBagEmdItems(string? basketDataJson)
    {
        var items = new List<(string, string, decimal, string)>();
        if (basketDataJson == null) return items;
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("bags", out var bagsEl) || bagsEl.ValueKind != JsonValueKind.Array) return items;
            foreach (var bag in bagsEl.EnumerateArray())
            {
                var paxId = bag.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "";
                var segId = bag.TryGetProperty("segmentId",   out var sid) ? sid.GetString() ?? "" : "";
                var price = bag.TryGetProperty("price",       out var p)   ? p.GetDecimal() : 0m;
                var tax   = bag.TryGetProperty("tax",         out var t)   ? t.GetDecimal() : 0m;
                var cur   = bag.TryGetProperty("currency",    out var c)   ? c.GetString() ?? "GBP" : "GBP";
                if (price > 0 && !string.IsNullOrEmpty(paxId) && !string.IsNullOrEmpty(segId))
                    items.Add((paxId, segId, price, cur));
            }
        }
        catch { }
        return items;
    }

    private static List<(string PassengerId, string SegmentId, decimal Amount, string Currency)> ParseProductEmdItems(string? basketDataJson)
    {
        var items = new List<(string, string, decimal, string)>();
        if (basketDataJson == null) return items;
        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("products", out var productsEl) || productsEl.ValueKind != JsonValueKind.Array) return items;
            foreach (var product in productsEl.EnumerateArray())
            {
                var paxId = product.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "";
                var segId = product.TryGetProperty("segmentId",   out var sid) ? sid.GetString() ?? "" : "";
                var price = product.TryGetProperty("price",       out var p)   ? p.GetDecimal() : 0m;
                var tax   = product.TryGetProperty("tax",         out var t)   ? t.GetDecimal() : 0m;
                var cur   = product.TryGetProperty("currency",    out var c)   ? c.GetString() ?? "GBP" : "GBP";
                if (price > 0)
                    items.Add((paxId, segId, price, cur));
            }
        }
        catch { }
        return items;
    }
}
