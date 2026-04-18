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

        var totalAmount = basket.TotalAmount ?? 0m;
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

        // 3. Initialise and authorise payment
        var paymentId = await _paymentServiceClient.InitialiseAsync(
            paymentType: "Fare",
            method: command.PaymentMethod,
            currencyCode: currency,
            amount: totalAmount,
            description: $"Booking payment — basket {command.BasketId}",
            cancellationToken);

        try
        {
            await _paymentServiceClient.AuthoriseAsync(
                paymentId, totalAmount,
                command.CardNumber, command.ExpiryDate, command.Cvv, command.CardholderName,
                cancellationToken);
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
            new { type = command.PaymentMethod, paymentReference = paymentId, amount = totalAmount }
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
            await _paymentServiceClient.VoidAsync(paymentId, "OrderConfirmationFailure", cancellationToken);
            await _orderServiceClient.DeleteDraftOrderAsync(draftOrder.OrderId, cancellationToken);
            throw;
        }

        // 5–8. Run post-confirm operations in parallel:
        //   - Hold + sell inventory (holds run in parallel across segments, then sell)
        //   - Issue e-tickets + write back to order
        //   - Settle payment
        //   - Link order to customer loyalty account
        var basketDataJson = basket.BasketData.HasValue ? basket.BasketData.Value.GetRawText() : null;

        var inventoryTask = RunInventorySellAsync(basketDataJson, draftOrder.OrderId, command.BasketId, bookingType == "Standby", cancellationToken);
        var ticketsTask   = RunTicketIssuanceAsync(basketDataJson, command, paymentId, totalAmount, currency, confirmedOrder, repricedOffers, cancellationToken);
        var settleTask    = _paymentServiceClient.SettleAsync(paymentId, totalAmount, cancellationToken);
        var customerTask  = RunCustomerLinkAsync(basketDataJson, confirmedOrder, cancellationToken);

        await Task.WhenAll(inventoryTask, ticketsTask, settleTask, customerTask);

        var issuedTickets = await ticketsTask;

        // Build flights from confirmed order items (which carry locked fares + tax lines).
        // Fall back to basket data parsing only if the Order MS returned no items.
        var flights = confirmedOrder.OrderItems.Count > 0
            ? BuildFlightsFromConfirmedItems(confirmedOrder.OrderItems)
            : ParseFlightsFromBasketData(basketDataJson);

        return new OrderResponse
        {
            BookingReference = confirmedOrder.BookingReference,
            Status = confirmedOrder.OrderStatus,
            CustomerId = string.Empty,
            Flights = flights,
            Passengers = ParsePassengersFromBasketData(basketDataJson),
            ETickets = issuedTickets.Select(t => new IssuedETicket
            {
                PassengerId = t.PassengerId,
                SegmentIds = t.SegmentIds,
                ETicketNumber = t.ETicketNumber
            }).ToList(),
            TotalPrice = confirmedOrder.TotalAmount ?? totalAmount,
            Currency = confirmedOrder.CurrencyCode,
            BookedAt = DateTime.UtcNow
        };
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

                payload.Add(new
                {
                    offerId        = offerId,
                    cabinCode      = item.CabinCode,
                    baseFareAmount = item.BaseFareAmount,
                    taxAmount      = item.TaxAmount,
                    totalAmount    = item.TotalAmount,
                    taxLines       = item.TaxLines?.Select(tl => new
                    {
                        code        = tl.Code,
                        amount      = tl.Amount,
                        description = tl.Description
                    })
                });
            }
        }
        catch { /* Return whatever was built */ }

        return payload;
    }

    /// <summary>
    /// Builds the Flights list from confirmed order items returned by the Order MS.
    /// These carry the locked fares and tax lines as stored in OrderData.
    /// </summary>
    private static List<OrderFlight> BuildFlightsFromConfirmedItems(
        IEnumerable<ConfirmedOrderItemResult> items)
    {
        return items.Select(item =>
        {
            var depDt = DateTime.TryParse($"{item.DepartureDate}T{item.DepartureTime}", out var d) ? d : default;
            var arrDt = DateTime.TryParse($"{item.DepartureDate}T{item.ArrivalTime}",   out var a) ? a : default;

            return new OrderFlight
            {
                FlightNumber     = item.FlightNumber,
                Origin           = item.Origin,
                Destination      = item.Destination,
                DepartureTime    = depDt,
                ArrivalTime      = arrDt,
                CabinClass       = item.CabinCode,
                FareFamily       = item.FareFamily,
                FareBasisCode    = item.FareBasisCode,
                BaseFareAmount   = item.BaseFareAmount,
                TaxAmount        = item.TaxAmount,
                TotalFareAmount  = item.TotalAmount,
                TaxLines         = item.TaxLines?.Select(tl => new OrderFlightTaxLine
                {
                    Code        = tl.Code,
                    Amount      = tl.Amount,
                    Description = tl.Description
                }).ToList()
            };
        }).ToList();
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

            var formOfPayment = BuildFormOfPayment(command, paymentId, totalAmount, currency);
            var fareConstruction = BuildFareConstruction(basketDataJson, repricedOffers, passengers.Count);
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

    private static List<OrderFlight> ParseFlightsFromBasketData(string? basketDataJson)
    {
        var flights = new List<OrderFlight>();
        if (basketDataJson == null) return flights;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("flightOffers", out var offersEl) &&
                offersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var offer in offersEl.EnumerateArray())
                {
                    var departureDate = offer.TryGetProperty("departureDate", out var ddv) ? ddv.GetString() ?? "" : "";
                    var departureTime = offer.TryGetProperty("departureTime", out var dtv) ? dtv.GetString() ?? "00:00" : "00:00";
                    var arrivalTime   = offer.TryGetProperty("arrivalTime",   out var atv) ? atv.GetString() ?? "00:00" : "00:00";

                    var depDt = DateTime.TryParse($"{departureDate}T{departureTime}", out var d) ? d : default;
                    var arrDt = DateTime.TryParse($"{departureDate}T{arrivalTime}",   out var a) ? a : default;

                    flights.Add(new OrderFlight
                    {
                        FlightNumber   = offer.TryGetProperty("flightNumber", out var v) ? v.GetString() ?? "" : "",
                        Origin         = offer.TryGetProperty("origin",       out v)     ? v.GetString() ?? "" : "",
                        Destination    = offer.TryGetProperty("destination",  out v)     ? v.GetString() ?? "" : "",
                        DepartureTime  = depDt,
                        ArrivalTime    = arrDt,
                        CabinClass     = offer.TryGetProperty("cabinCode",    out v)     ? v.GetString() ?? "" : ""
                    });
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return flights;
    }

    private static List<OrderPassenger> ParsePassengersFromBasketData(string? basketDataJson)
    {
        var passengers = new List<OrderPassenger>();
        if (basketDataJson == null) return passengers;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            // Build seat lookup: passengerId -> ordered list of seat numbers across all segments
            var seatsByPassenger = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("seats", out var seatsEl) && seatsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var seat in seatsEl.EnumerateArray())
                {
                    var paxId   = seat.TryGetProperty("passengerId",  out var spid) ? spid.GetString() ?? "" : "";
                    var seatNum = DecodeSeatNumber(seat.TryGetProperty("seatOfferId", out var soi) ? soi.GetString() : null) ?? "";
                    if (!string.IsNullOrEmpty(paxId) && !string.IsNullOrEmpty(seatNum))
                    {
                        if (!seatsByPassenger.ContainsKey(paxId))
                            seatsByPassenger[paxId] = [];
                        seatsByPassenger[paxId].Add(seatNum);
                    }
                }
            }

            // Build bag allowances lookup: passengerId -> list of formatted allowance strings
            var bagsByPassenger = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("orderItems", out var oiSource) && oiSource.ValueKind == JsonValueKind.Array)
            {
                foreach (var oi in oiSource.EnumerateArray())
                {
                    if (!oi.TryGetProperty("productType", out var ptEl) ||
                        !string.Equals(ptEl.GetString(), "BAG", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var paxId   = oi.TryGetProperty("passengerId",    out var bpid) ? bpid.GetString() ?? "" : "";
                    var addBags = oi.TryGetProperty("additionalBags", out var ab)   ? ab.GetInt32()          : 1;
                    if (!string.IsNullOrEmpty(paxId))
                    {
                        if (!bagsByPassenger.ContainsKey(paxId))
                            bagsByPassenger[paxId] = [];
                        bagsByPassenger[paxId].Add($"+{addBags} bag{(addBags == 1 ? "" : "s")}");
                    }
                }
            }

            if (root.TryGetProperty("passengers", out var passengersEl) &&
                passengersEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in passengersEl.EnumerateArray())
                {
                    var paxId = p.TryGetProperty("passengerId", out var pid) ? pid.GetString() ?? "" : "";

                    seatsByPassenger.TryGetValue(paxId, out var seats);
                    var seatNumber = seats is { Count: > 0 } ? string.Join(" / ", seats) : null;

                    bagsByPassenger.TryGetValue(paxId, out var bags);

                    passengers.Add(new OrderPassenger
                    {
                        PaxId          = paxId,
                        FirstName      = p.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "",
                        LastName       = p.TryGetProperty("surname",   out var sn) ? sn.GetString() ?? "" : "",
                        SeatNumber     = seatNumber,
                        BagAllowances  = bags ?? []
                    });
                }
            }
        }
        catch { /* Return whatever was parsed */ }

        return passengers;
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
                        SeatAssignments = seatsByInventoryId.TryGetValue(inventoryId, out var segSeats) ? segSeats : []
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
        Dictionary<Guid, RepriceOfferDto> repricedOffers,
        int numPassengers)
    {
        if (numPassengers <= 0) return null;

        try
        {
            using var doc = JsonDocument.Parse(basketDataJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("flightOffers", out var offersEl) ||
                offersEl.ValueKind != JsonValueKind.Array)
                return null;

            // Collect ordered per-segment fare components and aggregate taxes.
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

                // Per-passenger NUC component: round each one; NUC total = sum of rounded components.
                var perPaxComponent = Math.Round(item.BaseFareAmount / numPassengers, 2, MidpointRounding.AwayFromZero);
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

            // NUC total = sum of per-passenger component amounts (already rounded individually).
            var nucTotal = components.Sum(c => c.NucAmount);

            // Build per-passenger tax lines, dividing aggregated amounts by pax count.
            var taxLines = taxAccumulator.Select(kv => new TicketTaxLine
            {
                Code        = kv.Key,
                Amount      = Math.Round(kv.Value.Total / numPassengers, 2, MidpointRounding.AwayFromZero),
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
}
