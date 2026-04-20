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
        _logger.LogDebug("Closing inventory for flight {FlightNumber} on {DepartureDate}", command.FlightNumber, command.DepartureDate);
        await _offerServiceClient.CancelFlightInventoryAsync(command.FlightNumber, command.DepartureDate, ct);
        _logger.LogInformation("Inventory closed for flight {FlightNumber} on {DepartureDate} — no further sales permitted", command.FlightNumber, command.DepartureDate);

        // 2. Fetch flight details needed for replacement search
        _logger.LogDebug("Fetching flight inventory record for {FlightNumber} on {DepartureDate}", command.FlightNumber, command.DepartureDate);
        var flightInventory = await _offerServiceClient.GetFlightInventoryAsync(
            command.FlightNumber, command.DepartureDate, ct);

        if (flightInventory is null)
            throw new KeyNotFoundException(
                $"Flight {command.FlightNumber} on {command.DepartureDate} not found after inventory cancellation.");

        _logger.LogInformation(
            "Flight inventory retrieved for {FlightNumber} on {DepartureDate} — route {Origin}→{Destination}",
            command.FlightNumber, command.DepartureDate, flightInventory.Origin, flightInventory.Destination);

        // 3. Fetch all confirmed orders on this flight
        _logger.LogDebug("Fetching confirmed orders for flight {FlightNumber} on {DepartureDate}", command.FlightNumber, command.DepartureDate);
        var affectedOrders = await _orderServiceClient.GetOrdersByFlightAsync(
            command.FlightNumber, command.DepartureDate, "Confirmed", ct);

        _logger.LogInformation(
            "Order query returned {OrderCount} confirmed booking(s) on flight {FlightNumber} {DepartureDate}",
            affectedOrders.Orders.Count, command.FlightNumber, command.DepartureDate);

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
        _logger.LogDebug("Fetching manifest for flight {FlightNumber} on {DepartureDate}", command.FlightNumber, command.DepartureDate);
        var manifest = await _deliveryServiceClient.GetManifestAsync(
            command.FlightNumber, command.DepartureDate, ct);
        _logger.LogInformation(
            "Manifest retrieved for flight {FlightNumber} {DepartureDate} — {EntryCount} manifest entry/entries",
            command.FlightNumber, command.DepartureDate, manifest.Entries.Count);

        // 5. Sort bookings by IROPS priority: cabin class (F→J→W→Y), loyalty tier, booking date
        var sortedOrders = SortByPriority(affectedOrders.Orders);
        var totalPassengers = sortedOrders.Sum(o => o.Passengers.Count);
        _logger.LogInformation(
            "Sorted {OrderCount} booking(s) ({PassengerCount} passenger(s)) by IROPS priority (cabin→loyalty tier→booking date) for flight {FlightNumber} {DepartureDate}",
            sortedOrders.Count, totalPassengers, command.FlightNumber, command.DepartureDate);

        for (int i = 0; i < sortedOrders.Count; i++)
        {
            var o = sortedOrders[i];
            _logger.LogDebug(
                "Priority rank {Rank}: booking {BookingRef} cabin {CabinCode} loyalty {LoyaltyTier} booked {BookingDate} ({PaxCount} pax)",
                i + 1, o.BookingReference, o.Segment.CabinCode, o.LoyaltyTier ?? "none", o.BookingDate, o.Passengers.Count);
        }

        // 6. Search for replacement flights across the 72-hour lookahead window
        _logger.LogInformation(
            "Searching replacement flights for {Origin}→{Destination} over {LookaheadDays}-day window from {DepartureDate}",
            flightInventory.Origin, flightInventory.Destination, LookaheadDays, command.DepartureDate);
        var replacementOptions = await SearchAllReplacementsAsync(
            flightInventory.Origin, flightInventory.Destination, command.DepartureDate, ct);

        var directCount = replacementOptions.Count(o => o.IsDirect);
        var connectingCount = replacementOptions.Count(o => !o.IsDirect);
        _logger.LogInformation(
            "Replacement search complete for {Origin}→{Destination} — {TotalOptions} option(s) found ({DirectCount} direct, {ConnectingCount} connecting)",
            flightInventory.Origin, flightInventory.Destination, replacementOptions.Count, directCount, connectingCount);

        if (replacementOptions.Count == 0)
            _logger.LogWarning(
                "No replacement options found for {Origin}→{Destination} within {LookaheadDays}-day window — all affected bookings will be cancelled with IROPS refund",
                flightInventory.Origin, flightInventory.Destination, LookaheadDays);

        // 7. Process each booking, isolating failures so one bad booking doesn't block others
        var outcomes = new List<DisruptionPassengerOutcome>();
        for (int i = 0; i < sortedOrders.Count; i++)
        {
            var order = sortedOrders[i];
            _logger.LogInformation(
                "Processing booking {BookingRef} ({OrderIndex}/{OrderTotal}) — cabin {CabinCode}, {PaxCount} pax, loyalty {LoyaltyTier}",
                order.BookingReference, i + 1, sortedOrders.Count,
                order.Segment.CabinCode, order.Passengers.Count, order.LoyaltyTier ?? "none");
            try
            {
                var outcome = await ProcessOrderAsync(
                    order, command.FlightNumber, command.DepartureDate,
                    manifest, replacementOptions, ct);
                outcomes.Add(outcome);
                _logger.LogInformation(
                    "Booking {BookingRef} ({OrderIndex}/{OrderTotal}) outcome: {Outcome}",
                    order.BookingReference, i + 1, sortedOrders.Count, outcome.Outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IROPS processing failed for booking {BookingRef} ({OrderIndex}/{OrderTotal})",
                    order.BookingReference, i + 1, sortedOrders.Count);
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
            _logger.LogDebug(
                "Searching direct flights {Origin}→{Destination} on {SearchDate} (day offset +{DayOffset})",
                origin, destination, searchDate, dayOffset);

            try
            {
                var results = await _offerServiceClient.SearchFlightsAsync(origin, destination, searchDate, 1, ct);
                _logger.LogDebug(
                    "Direct search {Origin}→{Destination} on {SearchDate} returned {FlightCount} flight(s)",
                    origin, destination, searchDate, results.Flights.Count);

                int optionsBeforeDay = options.Count;
                foreach (var flight in results.Flights)
                {
                    var availableCabins = flight.Cabins.Where(c => c.SeatsAvailable > 0 && c.Fares.Count > 0).ToList();
                    _logger.LogDebug(
                        "Flight {FlightNumber} {DepartureDate} {DepartureTime}→{ArrivalTime}: {CabinCount} cabin(s) with availability ({Cabins})",
                        flight.FlightNumber, flight.DepartureDate, flight.DepartureTime, flight.ArrivalTime,
                        availableCabins.Count,
                        string.Join(", ", availableCabins.Select(c => $"{c.CabinCode}:{c.SeatsAvailable}")));

                    foreach (var cabin in availableCabins)
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

                _logger.LogInformation(
                    "Direct search {Origin}→{Destination} on {SearchDate} added {NewOptions} replacement option(s) (running total: {Total})",
                    origin, destination, searchDate, options.Count - optionsBeforeDay, options.Count);
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
            _logger.LogInformation(
                "No direct replacements found for {Origin}→{Destination} — falling back to connecting search via hub {HubAirport}",
                origin, destination, HubAirport);
            var connectingOptions = await SearchConnectingReplacementsAsync(origin, destination, baseDate, ct);
            _logger.LogInformation(
                "Connecting search via {HubAirport} found {ConnectingCount} option(s) for {Origin}→{Destination}",
                HubAirport, connectingOptions.Count, origin, destination);
            options.AddRange(connectingOptions);
        }
        else if (options.Count == 0)
        {
            _logger.LogWarning(
                "No direct replacements found for {Origin}→{Destination} and connecting search skipped (route is hub-adjacent)",
                origin, destination);
        }

        // Sort by departure time (earliest first) within each cabin
        options.Sort((a, b) =>
        {
            var dateCompare = string.Compare(a.DepartureDate, b.DepartureDate, StringComparison.Ordinal);
            if (dateCompare != 0) return dateCompare;
            return string.Compare(a.DepartureTime, b.DepartureTime, StringComparison.Ordinal);
        });

        _logger.LogDebug(
            "Replacement option list sorted by departure date/time — {TotalOptions} total option(s) for {Origin}→{Destination}",
            options.Count, origin, destination);

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

            _logger.LogDebug(
                "Searching leg 1 connecting flights {Origin}→{Hub} on {Date} (day offset +{DayOffset})",
                origin, HubAirport, leg1DateStr, dayOffset);

            OfferSearchResponse leg1Results;
            try
            {
                leg1Results = await _offerServiceClient.SearchFlightsAsync(origin, HubAirport, leg1DateStr, 1, ct);
                _logger.LogDebug(
                    "Leg 1 search {Origin}→{Hub} on {Date} returned {FlightCount} flight(s)",
                    origin, HubAirport, leg1DateStr, leg1Results.Flights.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Leg1 connecting search failed for {Origin}→{Hub} on {Date}",
                    origin, HubAirport, leg1DateStr);
                continue;
            }

            foreach (var leg1Flight in leg1Results.Flights)
            {
                int leg1ArrivalMinutes = TimeToMinutes(leg1Flight.ArrivalTime)
                    + leg1Flight.ArrivalDayOffset * 1440;

                _logger.LogDebug(
                    "Leg 1 candidate: {FlightNumber} departs {DepartureTime} arrives {ArrivalTime} (day+{ArrivalDayOffset}) at {Hub} — evaluating leg 2 options",
                    leg1Flight.FlightNumber, leg1Flight.DepartureTime, leg1Flight.ArrivalTime, leg1Flight.ArrivalDayOffset, HubAirport);

                // Search leg2 on same day and next day to cover overnight connections
                for (int leg2Offset = 0; leg2Offset <= 1; leg2Offset++)
                {
                    var leg2Date = leg1Date.AddDays(leg2Offset);
                    var leg2DateStr = leg2Date.ToString("yyyy-MM-dd");

                    _logger.LogDebug(
                        "Searching leg 2 connecting flights {Hub}→{Destination} on {Date} (leg2 offset +{Leg2Offset})",
                        HubAirport, destination, leg2DateStr, leg2Offset);

                    OfferSearchResponse leg2Results;
                    try
                    {
                        leg2Results = await _offerServiceClient.SearchFlightsAsync(
                            HubAirport, destination, leg2DateStr, 1, ct);
                        _logger.LogDebug(
                            "Leg 2 search {Hub}→{Destination} on {Date} returned {FlightCount} flight(s)",
                            HubAirport, destination, leg2DateStr, leg2Results.Flights.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Leg 2 connecting search failed for {Hub}→{Destination} on {Date}",
                            HubAirport, destination, leg2DateStr);
                        continue;
                    }

                    foreach (var leg2Flight in leg2Results.Flights)
                    {
                        int leg2DepartureMinutes = TimeToMinutes(leg2Flight.DepartureTime)
                            + leg2Offset * 1440;
                        int connectionMinutes = leg2DepartureMinutes - leg1ArrivalMinutes;

                        // Enforce minimum connection time at hub
                        if (connectionMinutes < MinConnectionMinutes)
                        {
                            _logger.LogDebug(
                                "Skipping connection {Leg1Flight}+{Leg2Flight} — connection time {ConnectionMinutes} min is below minimum {MinConnectionMinutes} min at {Hub}",
                                leg1Flight.FlightNumber, leg2Flight.FlightNumber,
                                connectionMinutes, MinConnectionMinutes, HubAirport);
                            continue;
                        }

                        foreach (var cabin1 in leg1Flight.Cabins.Where(c => c.SeatsAvailable > 0 && c.Fares.Count > 0))
                        {
                            var cabin2 = leg2Flight.Cabins.FirstOrDefault(c =>
                                c.CabinCode == cabin1.CabinCode && c.SeatsAvailable > 0 && c.Fares.Count > 0);

                            if (cabin2 is null)
                            {
                                _logger.LogDebug(
                                    "No matching cabin {CabinCode} with availability on leg 2 {Leg2Flight} {Leg2Date} — skipping connection",
                                    cabin1.CabinCode, leg2Flight.FlightNumber, leg2Flight.DepartureDate);
                                continue;
                            }

                            _logger.LogDebug(
                                "Valid connecting option found: {Leg1Flight}/{Leg1Date} + {Leg2Flight}/{Leg2Date} cabin {CabinCode} connection {ConnectionMinutes} min at {Hub}",
                                leg1Flight.FlightNumber, leg1Flight.DepartureDate,
                                leg2Flight.FlightNumber, leg2Flight.DepartureDate,
                                cabin1.CabinCode, connectionMinutes, HubAirport);

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

        _logger.LogDebug(
            "Finding best replacement for booking {BookingRef} — cabin {CabinCode}, {PaxCount} seat(s) needed, {OptionsAvailable} option(s) in pool",
            order.BookingReference, order.Segment.CabinCode, passengerCount, replacementOptions.Count);

        // Find best replacement for this booking's cabin, trying upgrades if same cabin unavailable
        var replacement = FindBestReplacement(replacementOptions, order.Segment.CabinCode, passengerCount);

        if (replacement is not null)
        {
            _logger.LogInformation(
                "Replacement found for booking {BookingRef}: {IsDirect} flight(s) {Flights} on {DepartureDate} cabin {CabinCode}",
                order.BookingReference,
                replacement.IsDirect ? "direct" : "connecting",
                string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
                replacement.DepartureDate,
                replacement.CabinCode);

            return await RebookOrderAsync(
                order, cancelledFlightNumber, cancelledDepartureDate,
                manifest, replacement, replacementOptions, ct);
        }

        _logger.LogInformation(
            "No suitable replacement found for booking {BookingRef} (cabin {CabinCode}, {PaxCount} seat(s)) within {LookaheadDays}-day window — proceeding to IROPS cancel with refund",
            order.BookingReference, order.Segment.CabinCode, passengerCount, LookaheadDays);

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

        _logger.LogInformation(
            "Rebook started for booking {BookingRef} — holding {LegCount} leg(s) for {PaxCount} pax on {Flights}",
            order.BookingReference, replacement.Legs.Count, passengerCount,
            string.Join("+", replacement.Legs.Select(l => $"{l.FlightNumber}/{l.DepartureDate}")));

        // Hold seats on each leg — if any hold fails, release already-held legs and fall through to cancel
        foreach (var leg in replacement.Legs)
        {
            _logger.LogDebug(
                "Holding {PaxCount} seat(s) in cabin {CabinCode} on inventory {InventoryId} ({FlightNumber}/{DepartureDate}) for booking {BookingRef}",
                passengerCount, replacement.CabinCode, leg.InventoryId, leg.FlightNumber, leg.DepartureDate, order.BookingReference);
            try
            {
                await _offerServiceClient.HoldInventoryAsync(
                    leg.InventoryId, replacement.CabinCode, passengerCount, order.BookingReference, ct);
                heldLegs.Add(leg);
                _logger.LogDebug(
                    "Hold succeeded on {FlightNumber}/{DepartureDate} inventory {InventoryId} for booking {BookingRef} — in-memory availability decremented",
                    leg.FlightNumber, leg.DepartureDate, leg.InventoryId, order.BookingReference);

                // Decrement in-memory availability so subsequent bookings don't over-allocate
                DecrementAvailability(allOptions, leg.InventoryId, replacement.CabinCode, passengerCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hold failed on leg {FlightNumber}/{Date} for booking {BookingRef} — releasing already held legs",
                    leg.FlightNumber, leg.DepartureDate, order.BookingReference);

                _logger.LogInformation(
                    "Releasing {HeldLegCount} already-held leg(s) after partial hold failure for booking {BookingRef}",
                    heldLegs.Count, order.BookingReference);

                foreach (var held in heldLegs)
                {
                    _logger.LogDebug(
                        "Releasing held inventory {InventoryId} ({FlightNumber}/{DepartureDate}) after hold failure for booking {BookingRef}",
                        held.InventoryId, held.FlightNumber, held.DepartureDate, order.BookingReference);
                    try { await _offerServiceClient.ReleaseInventoryAsync(held.InventoryId, replacement.CabinCode, passengerCount, ct); }
                    catch (Exception releaseEx)
                    {
                        _logger.LogError(releaseEx, "Failed to release inventory {InventoryId} ({FlightNumber}/{DepartureDate}) after partial hold failure for booking {BookingRef}",
                            held.InventoryId, held.FlightNumber, held.DepartureDate, order.BookingReference);
                    }
                }

                _logger.LogInformation(
                    "Falling back to IROPS cancel with refund for booking {BookingRef} after hold failure on {FlightNumber}/{Date}",
                    order.BookingReference, leg.FlightNumber, leg.DepartureDate);

                return await CancelOrderWithIropsRefundAsync(
                    order, cancelledFlightNumber, cancelledDepartureDate, manifest, ct);
            }
        }

        _logger.LogInformation(
            "All {LegCount} leg hold(s) succeeded for booking {BookingRef}",
            replacement.Legs.Count, order.BookingReference);

        // Adjust loyalty points for reward bookings where replacement costs fewer points
        if (order.BookingType == "Reward" && !string.IsNullOrEmpty(order.LoyaltyNumber))
        {
            var replacementPoints = replacement.Legs.Sum(l => (l.PointsPrice ?? 0)) * passengerCount;
            _logger.LogDebug(
                "Reward booking {BookingRef} — original points {OriginalPoints}, replacement points {ReplacementPoints} (loyalty {LoyaltyNumber})",
                order.BookingReference, order.TotalPointsAmount, replacementPoints, order.LoyaltyNumber);

            if (replacementPoints < order.TotalPointsAmount && replacementPoints > 0)
            {
                var surplus = order.TotalPointsAmount - replacementPoints;
                _logger.LogInformation(
                    "Reinstating {SurplusPoints} surplus points to loyalty account {LoyaltyNumber} for booking {BookingRef} (original {OriginalPoints}, replacement {ReplacementPoints})",
                    surplus, order.LoyaltyNumber, order.BookingReference, order.TotalPointsAmount, replacementPoints);
                try
                {
                    await _customerServiceClient.ReinstatePointsAsync(
                        order.LoyaltyNumber, surplus, order.BookingReference, "FlightCancellationRebook", ct);
                    _logger.LogInformation(
                        "Points reinstatement succeeded — {SurplusPoints} points reinstated to loyalty {LoyaltyNumber} for booking {BookingRef}",
                        surplus, order.LoyaltyNumber, order.BookingReference);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Points reinstatement failed for loyalty {LoyaltyNumber} on booking {BookingRef} — continuing with rebook",
                        order.LoyaltyNumber, order.BookingReference);
                }
            }
            else
            {
                _logger.LogDebug(
                    "No points reinstatement required for booking {BookingRef} — replacement points ({ReplacementPoints}) >= original ({OriginalPoints}) or replacement is zero",
                    order.BookingReference, replacementPoints, order.TotalPointsAmount);
            }
        }

        // Rebook the order (FlightCancellation reason waives all fare restrictions)
        _logger.LogInformation(
            "Submitting rebook request for booking {BookingRef}: segment {SegmentId} → {Flights} (reason: FlightCancellation, type: {BookingType})",
            order.BookingReference, order.Segment.SegmentId,
            string.Join("+", replacement.Legs.Select(l => $"{l.FlightNumber}/{l.DepartureDate}")),
            order.BookingType);

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
        _logger.LogInformation(
            "Order rebook committed for booking {BookingRef} — order service confirmed rebook to {Flights}",
            order.BookingReference,
            string.Join("+", replacement.Legs.Select(l => $"{l.FlightNumber}/{l.DepartureDate}")));

        // Convert held seats to sold on the replacement flight(s)
        foreach (var leg in replacement.Legs)
        {
            _logger.LogDebug(
                "Converting hold to sold on inventory {InventoryId} ({FlightNumber}/{DepartureDate}) for booking {BookingRef}",
                leg.InventoryId, leg.FlightNumber, leg.DepartureDate, order.BookingReference);
            try
            {
                await _offerServiceClient.SellInventoryAsync(
                    leg.InventoryId, replacement.CabinCode, passengerCount, order.BookingReference, ct);
                _logger.LogDebug(
                    "Inventory sold on {FlightNumber}/{DepartureDate} ({PaxCount} seat(s) cabin {CabinCode}) for booking {BookingRef}",
                    leg.FlightNumber, leg.DepartureDate, passengerCount, replacement.CabinCode, order.BookingReference);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sell inventory on leg {FlightNumber}/{Date} for booking {BookingRef} — seats remain held",
                    leg.FlightNumber, leg.DepartureDate, order.BookingReference);
            }
        }

        // Release sold seats from the original cancelled flight now the order has moved
        _logger.LogDebug(
            "Releasing original inventory {InventoryId} (cabin {CabinCode}, {PaxCount} seat(s)) for rebooked booking {BookingRef}",
            order.Segment.InventoryId, order.Segment.CabinCode, passengerCount, order.BookingReference);
        try
        {
            await _offerServiceClient.ReleaseInventoryAsync(
                order.Segment.InventoryId, order.Segment.CabinCode, passengerCount, ct);
            _logger.LogDebug(
                "Original inventory {InventoryId} released for booking {BookingRef}",
                order.Segment.InventoryId, order.BookingReference);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release original inventory for rebooked booking {BookingRef} — rebook already committed, continuing",
                order.BookingReference);
        }

        // Remove old manifest entries for the cancelled flight
        _logger.LogDebug(
            "Deleting manifest entries for booking {BookingRef} on cancelled flight {FlightNumber}/{DepartureDate}",
            order.BookingReference, cancelledFlightNumber, cancelledDepartureDate);
        await _deliveryServiceClient.DeleteManifestFlightAsync(
            order.BookingReference, cancelledFlightNumber, cancelledDepartureDate, ct);
        _logger.LogDebug(
            "Manifest entries deleted for booking {BookingRef} on {FlightNumber}/{DepartureDate}",
            order.BookingReference, cancelledFlightNumber, cancelledDepartureDate);

        // Reissue e-tickets for the replacement flight(s)
        var bookingManifestEntries = manifest.Entries
            .Where(e => e.BookingReference == order.BookingReference)
            .ToList();
        var cancelledTicketNumbers = bookingManifestEntries.Select(e => e.ETicketNumber).ToList();

        _logger.LogInformation(
            "Reissuing {TicketCount} e-ticket(s) for booking {BookingRef} — voiding [{CancelledTickets}] and issuing replacements for {LegCount} leg(s)",
            cancelledTicketNumbers.Count, order.BookingReference,
            string.Join(", ", cancelledTicketNumbers), replacement.Legs.Count);

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

        _logger.LogInformation(
            "E-ticket reissue complete for booking {BookingRef} — {NewTicketCount} new ticket(s) issued [{NewTickets}]",
            order.BookingReference, reissueResponse.Tickets.Count,
            string.Join(", ", reissueResponse.Tickets.Select(t => t.ETicketNumber)));

        // Write new manifest entries for each leg of the replacement itinerary
        foreach (var leg in replacement.Legs)
        {
            var manifestEntries = order.Passengers.Select(pax =>
            {
                var newTicket = reissueResponse.Tickets.FirstOrDefault(t => t.PassengerId == pax.PassengerId);
                var originalEntry = bookingManifestEntries.FirstOrDefault(e => e.PassengerId == pax.PassengerId);

                if (newTicket is null)
                    _logger.LogWarning(
                        "No reissued ticket found for passenger {PassengerId} on booking {BookingRef} leg {FlightNumber}/{DepartureDate} — manifest entry will have empty e-ticket number",
                        pax.PassengerId, order.BookingReference, leg.FlightNumber, leg.DepartureDate);

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

            _logger.LogDebug(
                "Writing {EntryCount} manifest entry/entries for booking {BookingRef} on leg {FlightNumber}/{DepartureDate} (inventory {InventoryId})",
                manifestEntries.Count, order.BookingReference, leg.FlightNumber, leg.DepartureDate, leg.InventoryId);

            await _deliveryServiceClient.WriteManifestAsync(new WriteManifestRequest
            {
                BookingReference = order.BookingReference,
                InventoryId = leg.InventoryId,
                FlightNumber = leg.FlightNumber,
                DepartureDate = leg.DepartureDate,
                Entries = manifestEntries
            }, ct);

            _logger.LogDebug(
                "Manifest written for booking {BookingRef} leg {FlightNumber}/{DepartureDate}",
                order.BookingReference, leg.FlightNumber, leg.DepartureDate);
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
        _logger.LogInformation(
            "IROPS cancel with refund started for booking {BookingRef} — type {BookingType}, {PaxCount} pax, paid {TotalPaid}, points {TotalPoints}",
            order.BookingReference, order.BookingType, order.Passengers.Count, order.TotalPaid, order.TotalPointsAmount);

        // Void all e-tickets for this booking
        var bookingTickets = manifest.Entries
            .Where(e => e.BookingReference == order.BookingReference)
            .Select(e => e.ETicketNumber)
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();

        _logger.LogInformation(
            "Voiding {TicketCount} e-ticket(s) for booking {BookingRef}: [{Tickets}]",
            bookingTickets.Count, order.BookingReference, string.Join(", ", bookingTickets));

        foreach (var ticketNumber in bookingTickets)
        {
            _logger.LogDebug("Voiding e-ticket {TicketNumber} (reason: IROPS) for booking {BookingRef}", ticketNumber, order.BookingReference);
            try
            {
                await _deliveryServiceClient.VoidTicketAsync(ticketNumber, "IROPS", ct);
                _logger.LogDebug("E-ticket {TicketNumber} voided for booking {BookingRef}", ticketNumber, order.BookingReference);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to void ticket {TicketNumber} for booking {BookingRef}",
                    ticketNumber, order.BookingReference);
            }
        }

        // Release inventory back to available
        _logger.LogDebug(
            "Releasing inventory {InventoryId} (cabin {CabinCode}, {PaxCount} seat(s)) for cancelled booking {BookingRef}",
            order.Segment.InventoryId, order.Segment.CabinCode, order.Passengers.Count, order.BookingReference);
        try
        {
            await _offerServiceClient.ReleaseInventoryAsync(
                order.Segment.InventoryId, order.Segment.CabinCode, order.Passengers.Count, ct);
            _logger.LogDebug(
                "Inventory {InventoryId} released for cancelled booking {BookingRef}",
                order.Segment.InventoryId, order.BookingReference);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release inventory for cancelled booking {BookingRef}", order.BookingReference);
        }

        // Cancel the order with full IROPS refund — IROPS policy overrides all fare conditions
        var pointsReinstated = order.BookingType == "Reward" ? order.TotalPointsAmount : 0;
        _logger.LogInformation(
            "Submitting IROPS cancel for booking {BookingRef} — refundable amount {RefundableAmount}, points reinstated {PointsReinstated}, payment {OriginalPaymentId}",
            order.BookingReference, order.TotalPaid, pointsReinstated, order.OriginalPaymentId);

        var cancelRequest = new CancelOrderRequest
        {
            Reason = "IROPS",
            CancellationFeeAmount = 0,
            RefundableAmount = order.TotalPaid,
            OriginalPaymentId = order.OriginalPaymentId,
            BookingType = order.BookingType,
            PointsReinstated = pointsReinstated
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
