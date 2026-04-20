using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionCancel;

public sealed class AdminDisruptionCancelHandler
{
    private const string HubAirport = "LHR";
    private const int MinConnectionMinutes = 60;
    private const int LookaheadDays = 3; // 72-hour window

    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly ILogger<AdminDisruptionCancelHandler> _logger;

    public AdminDisruptionCancelHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        CustomerServiceClient customerServiceClient,
        ILogger<AdminDisruptionCancelHandler> logger)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
        _customerServiceClient = customerServiceClient;
        _logger = logger;
    }

    public async Task<AdminDisruptionCancelResponse> HandleAsync(
        AdminDisruptionCancelCommand command,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "IROPS admin cancellation started for flight {FlightNumber} on {DepartureDate} (reason: {Reason})",
            command.FlightNumber, command.DepartureDate, command.Reason ?? "none");

        // 1. Close inventory immediately — prevents new bookings on the cancelled flight
        await _offerServiceClient.CancelFlightInventoryAsync(command.FlightNumber, command.DepartureDate, ct);
        _logger.LogInformation("Inventory closed for flight {FlightNumber} on {DepartureDate}", command.FlightNumber, command.DepartureDate);

        // 2. Fetch flight details needed for replacement search
        var flightInventory = await _offerServiceClient.GetFlightInventoryAsync(
            command.FlightNumber, command.DepartureDate, ct);

        if (flightInventory is null)
            throw new KeyNotFoundException(
                $"Flight {command.FlightNumber} on {command.DepartureDate} not found after inventory cancellation.");

        // 3. Fetch all confirmed orders on this flight
        var affectedOrders = await _orderServiceClient.GetOrdersByFlightAsync(
            command.FlightNumber, command.DepartureDate, "Confirmed", ct);

        if (affectedOrders.Orders.Count == 0)
        {
            _logger.LogInformation("Flight {FlightNumber} {DepartureDate} cancelled — no confirmed bookings to rebook.",
                command.FlightNumber, command.DepartureDate);

            return new AdminDisruptionCancelResponse
            {
                FlightNumber = command.FlightNumber,
                DepartureDate = command.DepartureDate,
                AffectedPassengerCount = 0,
                ProcessedAt = DateTime.UtcNow
            };
        }

        // 4. Fetch full manifest for manifest management
        var manifest = await _deliveryServiceClient.GetManifestAsync(
            command.FlightNumber, command.DepartureDate, ct);

        // 5. Sort bookings by IROPS priority: cabin class (F→J→W→Y), loyalty tier, booking date
        var sortedOrders = SortByPriority(affectedOrders.Orders);

        // 6. Search for replacement flights across the 72-hour lookahead window
        var replacementOptions = await SearchAllReplacementsAsync(
            flightInventory.Origin, flightInventory.Destination, command.DepartureDate, ct);

        // 7. Process each booking, isolating failures so one bad booking doesn't block others
        var outcomes = new List<DisruptionPassengerOutcome>();
        foreach (var order in sortedOrders)
        {
            try
            {
                var outcome = await ProcessOrderAsync(
                    order, command.FlightNumber, command.DepartureDate,
                    manifest, replacementOptions, ct);
                outcomes.Add(outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IROPS processing failed for booking {BookingRef}", order.BookingReference);
                outcomes.Add(new DisruptionPassengerOutcome
                {
                    BookingReference = order.BookingReference,
                    Outcome = "Failed",
                    FailureReason = ex.Message
                });
            }
        }

        var affectedPassengerCount = sortedOrders.Sum(o => o.Passengers.Count);
        var rebookedCount = outcomes.Count(o => o.Outcome == "Rebooked");
        var cancelledWithRefundCount = outcomes.Count(o => o.Outcome == "CancelledWithRefund");
        var failedCount = outcomes.Count(o => o.Outcome == "Failed");

        _logger.LogInformation(
            "IROPS admin cancellation complete for flight {FlightNumber} on {DepartureDate} — {AffectedPassengers} passengers: {Rebooked} rebooked, {CancelledWithRefund} cancelled with refund, {Failed} failed",
            command.FlightNumber, command.DepartureDate,
            affectedPassengerCount, rebookedCount, cancelledWithRefundCount, failedCount);

        if (failedCount > 0)
            _logger.LogWarning(
                "IROPS admin cancellation for flight {FlightNumber} on {DepartureDate} completed with {Failed} booking failure(s) requiring manual follow-up",
                command.FlightNumber, command.DepartureDate, failedCount);

        return new AdminDisruptionCancelResponse
        {
            FlightNumber = command.FlightNumber,
            DepartureDate = command.DepartureDate,
            AffectedPassengerCount = affectedPassengerCount,
            RebookedCount = rebookedCount,
            CancelledWithRefundCount = cancelledWithRefundCount,
            FailedCount = failedCount,
            Outcomes = outcomes,
            ProcessedAt = DateTime.UtcNow
        };
    }

    // ─── Replacement search ────────────────────────────────────────────────────

    private async Task<List<ReplacementOption>> SearchAllReplacementsAsync(
        string origin,
        string destination,
        string departureDateStr,
        CancellationToken ct)
    {
        var options = new List<ReplacementOption>();
        var baseDate = DateOnly.ParseExact(departureDateStr, "yyyy-MM-dd");

        for (int dayOffset = 0; dayOffset < LookaheadDays; dayOffset++)
        {
            var searchDate = baseDate.AddDays(dayOffset).ToString("yyyy-MM-dd");

            try
            {
                var results = await _offerServiceClient.SearchFlightsAsync(origin, destination, searchDate, 1, ct);
                foreach (var flight in results.Flights)
                {
                    foreach (var cabin in flight.Cabins.Where(c => c.SeatsAvailable > 0 && c.Fares.Count > 0))
                    {
                        options.Add(new ReplacementOption
                        {
                            IsDirect = true,
                            DepartureDate = flight.DepartureDate,
                            DepartureTime = flight.DepartureTime,
                            CabinCode = cabin.CabinCode,
                            Legs =
                            [
                                new ReplacementLeg
                                {
                                    OfferId = cabin.Fares[0].OfferId,
                                    InventoryId = cabin.Fares[0].InventoryId,
                                    FlightNumber = flight.FlightNumber,
                                    DepartureDate = flight.DepartureDate,
                                    DepartureTime = flight.DepartureTime,
                                    ArrivalTime = flight.ArrivalTime,
                                    ArrivalDayOffset = flight.ArrivalDayOffset,
                                    Origin = flight.Origin,
                                    Destination = flight.Destination,
                                    SeatsAvailable = cabin.SeatsAvailable,
                                    PointsPrice = cabin.Fares[0].PointsPrice
                                }
                            ]
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Replacement search failed for {Origin}→{Destination} on {Date}",
                    origin, destination, searchDate);
            }
        }

        // Search connecting via hub if no direct options found and route is not hub-adjacent
        if (options.Count == 0 && origin != HubAirport && destination != HubAirport)
        {
            var connectingOptions = await SearchConnectingReplacementsAsync(origin, destination, baseDate, ct);
            options.AddRange(connectingOptions);
        }

        // Sort by departure time (earliest first) within each cabin
        options.Sort((a, b) =>
        {
            var dateCompare = string.Compare(a.DepartureDate, b.DepartureDate, StringComparison.Ordinal);
            if (dateCompare != 0) return dateCompare;
            return string.Compare(a.DepartureTime, b.DepartureTime, StringComparison.Ordinal);
        });

        return options;
    }

    private async Task<List<ReplacementOption>> SearchConnectingReplacementsAsync(
        string origin,
        string destination,
        DateOnly baseDate,
        CancellationToken ct)
    {
        var options = new List<ReplacementOption>();

        for (int dayOffset = 0; dayOffset < LookaheadDays; dayOffset++)
        {
            var leg1Date = baseDate.AddDays(dayOffset);
            var leg1DateStr = leg1Date.ToString("yyyy-MM-dd");

            OfferSearchResponse leg1Results;
            try
            {
                leg1Results = await _offerServiceClient.SearchFlightsAsync(origin, HubAirport, leg1DateStr, 1, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Leg1 connecting search failed for {Origin}→{Hub} on {Date}",
                    origin, HubAirport, leg1DateStr);
                continue;
            }

            foreach (var leg1Flight in leg1Results.Flights)
            {
                // Search leg2 on same day and next day to cover overnight connections
                for (int leg2Offset = 0; leg2Offset <= 1; leg2Offset++)
                {
                    var leg2Date = leg1Date.AddDays(leg2Offset);
                    var leg2DateStr = leg2Date.ToString("yyyy-MM-dd");

                    OfferSearchResponse leg2Results;
                    try
                    {
                        leg2Results = await _offerServiceClient.SearchFlightsAsync(
                            HubAirport, destination, leg2DateStr, 1, ct);
                    }
                    catch
                    {
                        continue;
                    }

                    int leg1ArrivalMinutes = TimeToMinutes(leg1Flight.ArrivalTime)
                        + leg1Flight.ArrivalDayOffset * 1440;

                    foreach (var leg2Flight in leg2Results.Flights)
                    {
                        int leg2DepartureMinutes = TimeToMinutes(leg2Flight.DepartureTime)
                            + leg2Offset * 1440;

                        // Enforce minimum connection time at hub
                        if (leg2DepartureMinutes - leg1ArrivalMinutes < MinConnectionMinutes)
                            continue;

                        foreach (var cabin1 in leg1Flight.Cabins.Where(c => c.SeatsAvailable > 0 && c.Fares.Count > 0))
                        {
                            var cabin2 = leg2Flight.Cabins.FirstOrDefault(c =>
                                c.CabinCode == cabin1.CabinCode && c.SeatsAvailable > 0 && c.Fares.Count > 0);
                            if (cabin2 is null) continue;

                            options.Add(new ReplacementOption
                            {
                                IsDirect = false,
                                DepartureDate = leg1Flight.DepartureDate,
                                DepartureTime = leg1Flight.DepartureTime,
                                CabinCode = cabin1.CabinCode,
                                Legs =
                                [
                                    new ReplacementLeg
                                    {
                                        OfferId = cabin1.Fares[0].OfferId,
                                        InventoryId = cabin1.Fares[0].InventoryId,
                                        FlightNumber = leg1Flight.FlightNumber,
                                        DepartureDate = leg1Flight.DepartureDate,
                                        DepartureTime = leg1Flight.DepartureTime,
                                        ArrivalTime = leg1Flight.ArrivalTime,
                                        ArrivalDayOffset = leg1Flight.ArrivalDayOffset,
                                        Origin = leg1Flight.Origin,
                                        Destination = leg1Flight.Destination,
                                        SeatsAvailable = cabin1.SeatsAvailable,
                                        PointsPrice = cabin1.Fares[0].PointsPrice
                                    },
                                    new ReplacementLeg
                                    {
                                        OfferId = cabin2.Fares[0].OfferId,
                                        InventoryId = cabin2.Fares[0].InventoryId,
                                        FlightNumber = leg2Flight.FlightNumber,
                                        DepartureDate = leg2Flight.DepartureDate,
                                        DepartureTime = leg2Flight.DepartureTime,
                                        ArrivalTime = leg2Flight.ArrivalTime,
                                        ArrivalDayOffset = leg2Flight.ArrivalDayOffset,
                                        Origin = leg2Flight.Origin,
                                        Destination = leg2Flight.Destination,
                                        SeatsAvailable = cabin2.SeatsAvailable,
                                        PointsPrice = cabin2.Fares[0].PointsPrice
                                    }
                                ]
                            });
                        }
                    }
                }
            }
        }

        return options;
    }

    // ─── Per-order processing ──────────────────────────────────────────────────

    private async Task<DisruptionPassengerOutcome> ProcessOrderAsync(
        AffectedOrderDto order,
        string cancelledFlightNumber,
        string cancelledDepartureDate,
        ManifestResponse manifest,
        List<ReplacementOption> replacementOptions,
        CancellationToken ct)
    {
        var passengerCount = order.Passengers.Count;

        // Find best replacement for this booking's cabin, trying upgrades if same cabin unavailable
        var replacement = FindBestReplacement(replacementOptions, order.Segment.CabinCode, passengerCount);

        if (replacement is not null)
        {
            return await RebookOrderAsync(
                order, cancelledFlightNumber, cancelledDepartureDate,
                manifest, replacement, replacementOptions, ct);
        }

        // No replacement found within 72-hour window — IROPS cancel with full refund
        return await CancelOrderWithIropsRefundAsync(
            order, cancelledFlightNumber, cancelledDepartureDate, manifest, ct);
    }

    private async Task<DisruptionPassengerOutcome> RebookOrderAsync(
        AffectedOrderDto order,
        string cancelledFlightNumber,
        string cancelledDepartureDate,
        ManifestResponse manifest,
        ReplacementOption replacement,
        List<ReplacementOption> allOptions,
        CancellationToken ct)
    {
        var passengerCount = order.Passengers.Count;
        var heldLegs = new List<ReplacementLeg>();

        // Hold seats on each leg — if any hold fails, release already-held legs and fall through to cancel
        foreach (var leg in replacement.Legs)
        {
            try
            {
                await _offerServiceClient.HoldInventoryAsync(
                    leg.InventoryId, replacement.CabinCode, passengerCount, order.BookingReference, ct);
                heldLegs.Add(leg);

                // Decrement in-memory availability so subsequent bookings don't over-allocate
                DecrementAvailability(allOptions, leg.InventoryId, replacement.CabinCode, passengerCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hold failed on leg {FlightNumber}/{Date} for booking {BookingRef} — releasing already held legs",
                    leg.FlightNumber, leg.DepartureDate, order.BookingReference);

                foreach (var held in heldLegs)
                {
                    try { await _offerServiceClient.ReleaseInventoryAsync(held.InventoryId, replacement.CabinCode, passengerCount, ct); }
                    catch (Exception releaseEx)
                    {
                        _logger.LogError(releaseEx, "Failed to release inventory after partial hold failure for booking {BookingRef}", order.BookingReference);
                    }
                }

                return await CancelOrderWithIropsRefundAsync(
                    order, cancelledFlightNumber, cancelledDepartureDate, manifest, ct);
            }
        }

        // Adjust loyalty points for reward bookings where replacement costs fewer points
        if (order.BookingType == "Reward" && !string.IsNullOrEmpty(order.LoyaltyNumber))
        {
            var replacementPoints = replacement.Legs.Sum(l => (l.PointsPrice ?? 0)) * passengerCount;
            if (replacementPoints < order.TotalPointsAmount && replacementPoints > 0)
            {
                var surplus = order.TotalPointsAmount - replacementPoints;
                try
                {
                    await _customerServiceClient.ReinstatePointsAsync(
                        order.LoyaltyNumber, surplus, order.BookingReference, "FlightCancellationRebook", ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Points reinstatement failed for loyalty {LoyaltyNumber} on booking {BookingRef} — continuing with rebook",
                        order.LoyaltyNumber, order.BookingReference);
                }
            }
        }

        // Rebook the order (FlightCancellation reason waives all fare restrictions)
        var rebookRequest = new RebookOrderRequest
        {
            CancelledSegmentId = order.Segment.SegmentId,
            ReplacementOfferIds = replacement.Legs.Select(l => l.OfferId).ToList(),
            Reason = "FlightCancellation",
            BookingType = order.BookingType,
            FromFlightNumber = cancelledFlightNumber,
            FromDepartureDate = cancelledDepartureDate,
            ToFlights = replacement.Legs.Select(l => new RebookToFlightDto
            {
                FlightNumber = l.FlightNumber,
                DepartureDate = l.DepartureDate
            }).ToList()
        };
        await _orderServiceClient.RebookOrderAsync(order.BookingReference, rebookRequest, ct);

        // Convert held seats to sold on the replacement flight(s)
        foreach (var leg in replacement.Legs)
        {
            try
            {
                await _offerServiceClient.SellInventoryAsync(
                    leg.InventoryId, replacement.CabinCode, passengerCount, order.BookingReference, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sell inventory on leg {FlightNumber}/{Date} for booking {BookingRef} — seats remain held",
                    leg.FlightNumber, leg.DepartureDate, order.BookingReference);
            }
        }

        // Release sold seats from the original cancelled flight now the order has moved
        try
        {
            await _offerServiceClient.ReleaseInventoryAsync(
                order.Segment.InventoryId, order.Segment.CabinCode, passengerCount, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release original inventory for rebooked booking {BookingRef} — rebook already committed, continuing",
                order.BookingReference);
        }

        // Remove old manifest entries for the cancelled flight
        await _deliveryServiceClient.DeleteManifestFlightAsync(
            order.BookingReference, cancelledFlightNumber, cancelledDepartureDate, ct);

        // Reissue e-tickets for the replacement flight(s)
        var bookingManifestEntries = manifest.Entries
            .Where(e => e.BookingReference == order.BookingReference)
            .ToList();
        var cancelledTicketNumbers = bookingManifestEntries.Select(e => e.ETicketNumber).ToList();

        var reissueRequest = new ReissueTicketsRequest
        {
            BookingReference = order.BookingReference,
            CancelledETicketNumbers = cancelledTicketNumbers,
            ReplacementSegments = replacement.Legs.Select(l => new ReissueSegmentDto
            {
                InventoryId = l.InventoryId,
                FlightNumber = l.FlightNumber,
                DepartureDate = l.DepartureDate,
                Origin = l.Origin,
                Destination = l.Destination,
                CabinCode = replacement.CabinCode
            }).ToList()
        };
        var reissueResponse = await _deliveryServiceClient.ReissueTicketsAsync(reissueRequest, ct);

        // Write new manifest entries for each leg of the replacement itinerary
        foreach (var leg in replacement.Legs)
        {
            var manifestEntries = order.Passengers.Select(pax =>
            {
                var newTicket = reissueResponse.Tickets.FirstOrDefault(t => t.PassengerId == pax.PassengerId);
                var originalEntry = bookingManifestEntries.FirstOrDefault(e => e.PassengerId == pax.PassengerId);
                return new WriteManifestEntryDto
                {
                    PassengerId = pax.PassengerId,
                    GivenName = pax.GivenName,
                    Surname = pax.Surname,
                    ETicketNumber = newTicket?.ETicketNumber ?? string.Empty,
                    SeatNumber = null, // seat re-selection handled by passenger via manage-booking
                    CabinCode = replacement.CabinCode,
                    SeatPosition = originalEntry?.SeatPosition
                };
            }).ToList();

            await _deliveryServiceClient.WriteManifestAsync(new WriteManifestRequest
            {
                BookingReference = order.BookingReference,
                InventoryId = leg.InventoryId,
                FlightNumber = leg.FlightNumber,
                DepartureDate = leg.DepartureDate,
                Entries = manifestEntries
            }, ct);
        }

        _logger.LogInformation(
            "Booking {BookingRef} rebooked onto {Flights} departing {Date}",
            order.BookingReference,
            string.Join(" + ", replacement.Legs.Select(l => l.FlightNumber)),
            replacement.DepartureDate);

        return new DisruptionPassengerOutcome
        {
            BookingReference = order.BookingReference,
            Outcome = "Rebooked",
            ReplacementFlightNumber = string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
            ReplacementDepartureDate = replacement.DepartureDate
        };
    }

    private async Task<DisruptionPassengerOutcome> CancelOrderWithIropsRefundAsync(
        AffectedOrderDto order,
        string cancelledFlightNumber,
        string cancelledDepartureDate,
        ManifestResponse manifest,
        CancellationToken ct)
    {
        // Void all e-tickets for this booking
        var bookingTickets = manifest.Entries
            .Where(e => e.BookingReference == order.BookingReference)
            .Select(e => e.ETicketNumber)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct();

        foreach (var ticketNumber in bookingTickets)
        {
            try { await _deliveryServiceClient.VoidTicketAsync(ticketNumber, "IROPS", ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to void ticket {TicketNumber} for booking {BookingRef}",
                    ticketNumber, order.BookingReference);
            }
        }

        // Release inventory back to available
        try
        {
            await _offerServiceClient.ReleaseInventoryAsync(
                order.Segment.InventoryId, order.Segment.CabinCode, order.Passengers.Count, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release inventory for cancelled booking {BookingRef}", order.BookingReference);
        }

        // Cancel the order with full IROPS refund — IROPS policy overrides all fare conditions
        var cancelRequest = new CancelOrderRequest
        {
            Reason = "IROPS",
            CancellationFeeAmount = 0,
            RefundableAmount = order.TotalPaid,
            OriginalPaymentId = order.OriginalPaymentId,
            BookingType = order.BookingType,
            PointsReinstated = order.BookingType == "Reward" ? order.TotalPointsAmount : 0
        };
        await _orderServiceClient.CancelOrderIropsAsync(order.BookingReference, cancelRequest, ct);

        _logger.LogInformation(
            "Booking {BookingRef} cancelled with IROPS refund — no replacement found within {Days}-day window",
            order.BookingReference, LookaheadDays);

        return new DisruptionPassengerOutcome
        {
            BookingReference = order.BookingReference,
            Outcome = "CancelledWithRefund"
        };
    }

    // ─── Priority sorting ──────────────────────────────────────────────────────

    private static List<AffectedOrderDto> SortByPriority(IReadOnlyList<AffectedOrderDto> orders) =>
        orders
            .OrderBy(o => CabinPriority(o.Segment.CabinCode))
            .ThenBy(o => LoyaltyTierPriority(o.LoyaltyTier))
            .ThenBy(o => o.BookingDate)
            .ToList();

    private static int CabinPriority(string cabinCode) => cabinCode switch
    {
        "F" => 0,
        "J" => 1,
        "W" => 2,
        "Y" => 3,
        _ => 4
    };

    private static int LoyaltyTierPriority(string? tier) => tier switch
    {
        "Platinum" => 0,
        "Gold" => 1,
        "Silver" => 2,
        "Blue" => 3,
        _ => 4
    };

    // ─── Replacement selection ─────────────────────────────────────────────────

    private static ReplacementOption? FindBestReplacement(
        List<ReplacementOption> options,
        string originalCabinCode,
        int seatsNeeded)
    {
        foreach (var cabinCode in GetCabinSearchOrder(originalCabinCode))
        {
            var candidate = options.FirstOrDefault(o =>
                o.CabinCode == cabinCode &&
                o.Legs.All(l => l.SeatsAvailable >= seatsNeeded));

            if (candidate is not null)
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> GetCabinSearchOrder(string originalCabin) => originalCabin switch
    {
        "Y" => ["Y", "W", "J", "F"],
        "W" => ["W", "J", "F"],
        "J" => ["J", "F"],
        "F" => ["F"],
        _ => [originalCabin]
    };

    private static void DecrementAvailability(
        List<ReplacementOption> options,
        Guid inventoryId,
        string cabinCode,
        int seats)
    {
        foreach (var option in options)
        {
            if (option.CabinCode != cabinCode) continue;
            foreach (var leg in option.Legs)
            {
                if (leg.InventoryId == inventoryId)
                    leg.SeatsAvailable = Math.Max(0, leg.SeatsAvailable - seats);
            }
        }
    }

    private static int TimeToMinutes(string time)
    {
        var parts = time.Split(':');
        return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
    }
}

// ─── Internal replacement model ────────────────────────────────────────────────

internal sealed class ReplacementOption
{
    public bool IsDirect { get; init; }
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public List<ReplacementLeg> Legs { get; init; } = [];
}

internal sealed class ReplacementLeg
{
    public string OfferId { get; init; } = string.Empty;
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public int SeatsAvailable { get; set; }
    public int? PointsPrice { get; init; }
}
