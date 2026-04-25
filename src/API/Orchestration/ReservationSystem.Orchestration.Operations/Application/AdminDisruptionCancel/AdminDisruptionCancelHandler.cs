using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionCancel;

public sealed class AdminDisruptionCancelHandler
{
    private const int AvailabilityLookaheadDays = 7;
    private const int CheckInCutoffMinutes = 45;

    private readonly OfferServiceClient _offerServiceClient;
    private readonly OrderServiceClient _orderServiceClient;
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<AdminDisruptionCancelHandler> _logger;

    public AdminDisruptionCancelHandler(
        OfferServiceClient offerServiceClient,
        OrderServiceClient orderServiceClient,
        DeliveryServiceClient deliveryServiceClient,
        ILogger<AdminDisruptionCancelHandler> logger)
    {
        _offerServiceClient = offerServiceClient;
        _orderServiceClient = orderServiceClient;
        _deliveryServiceClient = deliveryServiceClient;
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
        var flightInventory = await _offerServiceClient.GetFlightInventoryAsync(command.FlightNumber, command.DepartureDate, ct);
        if (flightInventory is null)
            throw new KeyNotFoundException($"Flight {command.FlightNumber} on {command.DepartureDate} not found after inventory cancellation.");

        var origin = flightInventory.Origin;
        var destination = flightInventory.Destination;

        _logger.LogInformation("Route confirmed: {Origin}→{Destination}", origin, destination);

        // 3. Fetch manifest — contains the OrderIds of all bookings on this flight for an efficient indexed lookup
        var manifest = await _deliveryServiceClient.GetManifestAsync(command.FlightNumber, command.DepartureDate, ct);

        // 4. Fetch confirmed orders using OrderIds from the manifest (indexed PK lookup, avoids a full table JSON scan)
        var orderIds = manifest.Entries
            .Select(e => e.OrderId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        AffectedOrdersResponse affectedOrders;
        if (orderIds.Count == 0)
        {
            affectedOrders = new AffectedOrdersResponse();
        }
        else
        {
            affectedOrders = await _orderServiceClient.GetAffectedOrdersByIdsAsync(
                orderIds, command.FlightNumber, command.DepartureDate, ct);
        }

        _logger.LogInformation(
            "{OrderCount} confirmed booking(s) on flight {FlightNumber} {DepartureDate}",
            affectedOrders.Orders.Count, command.FlightNumber, command.DepartureDate);

        if (affectedOrders.Orders.Count == 0)
        {
            return new AdminDisruptionCancelResponse
            {
                FlightNumber = command.FlightNumber,
                DepartureDate = command.DepartureDate,
                AffectedPassengerCount = 0,
                ProcessedAt = DateTime.UtcNow
            };
        }

        // 5. Sort by IROPS priority: cabin class (F→J→W→Y), loyalty tier, booking date
        var sortedOrders = SortByPriority(affectedOrders.Orders);
        var totalPassengers = sortedOrders.Sum(o => o.Passengers.Count);

        _logger.LogInformation(
            "Processing {OrderCount} booking(s) ({PassengerCount} passenger(s)) by IROPS priority",
            sortedOrders.Count, totalPassengers);

        // 6. Fetch all available flights on the route for the next week in a single call.
        //    Availability is a lightweight read — no fare pricing, no stored offers created.
        var availability = await _offerServiceClient.GetFlightAvailabilityAsync(
            origin, destination, command.DepartureDate, AvailabilityLookaheadDays, ct);

        var replacementPool = availability.Flights
            .SelectMany(flight => flight.Cabins
                .Select(cabin => new ReplacementOption
                {
                    DepartureDate    = flight.DepartureDate,
                    DepartureTime    = flight.DepartureTime,
                    DepartureTimeUtc = flight.DepartureTimeUtc,
                    CabinCode        = cabin.CabinCode,
                    Legs             =
                    [
                        new ReplacementLeg
                        {
                            OfferId          = string.Empty,
                            InventoryId      = flight.InventoryId,
                            FlightNumber     = flight.FlightNumber,
                            DepartureDate    = flight.DepartureDate,
                            DepartureTime    = flight.DepartureTime,
                            ArrivalTime      = flight.ArrivalTime,
                            ArrivalDayOffset = flight.ArrivalDayOffset,
                            Origin           = flight.Origin,
                            Destination      = flight.Destination,
                            SeatsAvailable   = cabin.SeatsAvailable,
                            PointsPrice      = null
                        }
                    ]
                }))
            .Where(o => IsRebookable(o.DepartureDate, o.DepartureTimeUtc))
            .ToList();

        _logger.LogInformation(
            "Availability search returned {FlightCount} flight(s) across {Days} days for {Origin}→{Destination}; {PoolCount} option(s) remain after check-in cutoff filter",
            availability.Flights.Count, AvailabilityLookaheadDays, origin, destination, replacementPool.Count);

        var outcomes = new List<DisruptionPassengerOutcome>();
        for (int i = 0; i < sortedOrders.Count; i++)
        {
            var order = sortedOrders[i];
            _logger.LogInformation(
                "Processing booking {BookingRef} ({Index}/{Total}) — cabin {CabinCode}, {PaxCount} pax",
                order.BookingReference, i + 1, sortedOrders.Count, order.Segment.CabinCode, order.Passengers.Count);
            try
            {
                var replacement = FindBestReplacement(replacementPool, order.Segment.CabinCode, order.Passengers.Count);

                DisruptionPassengerOutcome outcome;
                if (replacement is not null)
                {
                    _logger.LogInformation(
                        "Replacement found for booking {BookingRef}: {FlightNumber} on {DepartureDate} cabin {CabinCode}",
                        order.BookingReference,
                        string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
                        replacement.DepartureDate, replacement.CabinCode);

                    outcome = await RebookOrderAsync(
                        order, command.FlightNumber, command.DepartureDate,
                        manifest, replacement, replacementPool, ct);
                }
                else
                {
                    _logger.LogError(
                        "No replacement found for booking {BookingRef} — no available {CabinCode} flights from {Origin} to {Destination} within {Days} days",
                        order.BookingReference, order.Segment.CabinCode, origin, destination, AvailabilityLookaheadDays);

                    outcome = new DisruptionPassengerOutcome
                    {
                        BookingReference = order.BookingReference,
                        Outcome = "Failed",
                        FailureReason = $"No available flights from {origin} to {destination} within {AvailabilityLookaheadDays} days"
                    };
                }

                outcomes.Add(outcome);
                _logger.LogInformation("Booking {BookingRef} outcome: {Outcome}", order.BookingReference, outcome.Outcome);
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
        var failedCount = outcomes.Count(o => o.Outcome == "Failed");

        _logger.LogInformation(
            "IROPS cancellation complete for {FlightNumber} {DepartureDate} — {AffectedPassengers} pax: {Rebooked} rebooked, {Failed} failed",
            command.FlightNumber, command.DepartureDate, affectedPassengerCount, rebookedCount, failedCount);

        return new AdminDisruptionCancelResponse
        {
            FlightNumber = command.FlightNumber,
            DepartureDate = command.DepartureDate,
            AffectedPassengerCount = affectedPassengerCount,
            RebookedCount = rebookedCount,
            CancelledWithRefundCount = 0,
            FailedCount = failedCount,
            Outcomes = outcomes,
            ProcessedAt = DateTime.UtcNow
        };
    }

    // ─── Per-order rebooking ───────────────────────────────────────────────────

    private async Task<DisruptionPassengerOutcome> RebookOrderAsync(
        AffectedOrderDto order,
        string cancelledFlightNumber,
        string cancelledDepartureDate,
        ManifestResponse manifest,
        ReplacementOption replacement,
        List<ReplacementOption> allOptions,
        CancellationToken ct)
    {
        var passengerIds = order.Passengers.Select(p => p.PassengerId).ToList();
        var heldLegs = new List<ReplacementLeg>();

        // Hold seats on each leg; release all held legs and return Failed if any hold fails
        foreach (var leg in replacement.Legs)
        {
            try
            {
                await _offerServiceClient.HoldInventoryAsync(
                    leg.InventoryId, replacement.CabinCode, passengerIds, order.OrderId, ct);
                heldLegs.Add(leg);
                DecrementAvailability(allOptions, leg.InventoryId, replacement.CabinCode, passengerIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hold failed on {FlightNumber}/{Date} for booking {BookingRef} — releasing {HeldCount} held leg(s)",
                    leg.FlightNumber, leg.DepartureDate, order.BookingReference, heldLegs.Count);

                foreach (var held in heldLegs)
                {
                    try { await _offerServiceClient.ReleaseInventoryAsync(held.InventoryId, replacement.CabinCode, passengerIds.Count, order.OrderId, ct); }
                    catch (Exception releaseEx)
                    {
                        _logger.LogError(releaseEx, "Failed to release inventory {InventoryId} after hold failure for booking {BookingRef}",
                            held.InventoryId, order.BookingReference);
                    }
                }

                return new DisruptionPassengerOutcome
                {
                    BookingReference = order.BookingReference,
                    Outcome = "Failed",
                    FailureReason = $"Inventory hold failed on {leg.FlightNumber}/{leg.DepartureDate}: {ex.Message}"
                };
            }
        }

        // Rebook the affected segment only (FlightCancellation reason waives fare restrictions)
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
                DepartureDate = l.DepartureDate,
                InventoryId = l.InventoryId.ToString()
            }).ToList()
        };

        try
        {
            await _orderServiceClient.RebookOrderAsync(order.BookingReference, rebookRequest, ct);
        }
        catch
        {
            // Rebook failed — release held seats and propagate so outer handler marks as Failed
            foreach (var held in heldLegs)
            {
                try { await _offerServiceClient.ReleaseInventoryAsync(held.InventoryId, replacement.CabinCode, passengerIds.Count, order.OrderId, ct); }
                catch (Exception releaseEx)
                {
                    _logger.LogError(releaseEx, "Failed to release inventory {InventoryId} after rebook failure for booking {BookingRef}",
                        held.InventoryId, order.BookingReference);
                }
            }
            throw;
        }

        // Rebook committed — atomically sell replacement seats and release the original in one call
        var toItems = replacement.Legs
            .Select(l => (l.InventoryId, replacement.CabinCode))
            .ToList();

        try
        {
            await _offerServiceClient.RebookInventoryAsync(
                order.Segment.InventoryId, order.Segment.CabinCode, toItems, order.OrderId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rebook inventory for booking {BookingRef} — seats may remain in inconsistent state",
                order.BookingReference);
        }

        // Reissue e-tickets for the replacement segment(s)
        var existingTickets = await _deliveryServiceClient.GetTicketsByBookingAsync(order.BookingReference, ct);
        var ticketsToVoid = existingTickets.Where(t => !t.IsVoided).Select(t => t.ETicketNumber).ToList();

        var reissueRequest = new ReissueTicketsRequest
        {
            BookingReference = order.BookingReference,
            CancelledETicketNumbers = ticketsToVoid,
            Passengers = order.Passengers.Select(pax => new ReissuePassengerDto
            {
                PassengerId = pax.PassengerId,
                GivenName = pax.GivenName,
                Surname = pax.Surname,
                PassengerTypeCode = pax.PassengerType
            }).ToList(),
            Segments = replacement.Legs.Select(l => new ReissueSegmentDto
            {
                InventoryId = l.InventoryId,
                FlightNumber = l.FlightNumber,
                DepartureDate = l.DepartureDate,
                DepartureTime = l.DepartureTime,
                Origin = l.Origin,
                Destination = l.Destination,
                CabinCode = replacement.CabinCode
            }).ToList(),
            Reason = "FlightCancellation",
            Actor = "OperationsAPI"
        };
        var reissueResponse = await _deliveryServiceClient.ReissueTicketsAsync(reissueRequest, ct);

        // Update manifest entries in place for each replacement leg
        foreach (var replacementLeg in replacement.Legs)
        {
            var passengers = order.Passengers.Select(pax =>
            {
                var newTicket = reissueResponse.Tickets.FirstOrDefault(t => t.PassengerId == pax.PassengerId);

                if (newTicket is null)
                    _logger.LogWarning(
                        "No reissued ticket found for passenger {PassengerId} on booking {BookingRef} leg {FlightNumber}",
                        pax.PassengerId, order.BookingReference, replacementLeg.FlightNumber);

                return new RebookManifestPassengerDto
                {
                    PassengerId   = pax.PassengerId,
                    ETicketNumber = newTicket?.ETicketNumber ?? string.Empty
                };
            }).ToList();

            await _deliveryServiceClient.RebookManifestAsync(
                order.BookingReference,
                cancelledFlightNumber,
                cancelledDepartureDate,
                new RebookManifestRequest
                {
                    ToInventoryId    = replacementLeg.InventoryId,
                    ToFlightNumber   = replacementLeg.FlightNumber,
                    ToOrigin         = replacementLeg.Origin,
                    ToDestination    = replacementLeg.Destination,
                    ToDepartureDate  = replacementLeg.DepartureDate,
                    ToDepartureTime  = replacementLeg.DepartureTime,
                    ToArrivalTime    = replacementLeg.ArrivalTime,
                    ToCabinCode      = replacement.CabinCode,
                    Passengers       = passengers
                },
                ct);
        }

        _logger.LogInformation(
            "Booking {BookingRef} segment rebooked: {CancelledFlight} → {ReplacementFlights} on {ReplacementDate}",
            order.BookingReference, cancelledFlightNumber,
            string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
            replacement.DepartureDate);

        return new DisruptionPassengerOutcome
        {
            BookingReference = order.BookingReference,
            Outcome = "Rebooked",
            ReplacementFlightNumber = string.Join("+", replacement.Legs.Select(l => l.FlightNumber)),
            ReplacementDepartureDate = replacement.DepartureDate
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

    private static bool IsRebookable(string departureDate, string? departureTimeUtc)
    {
        if (departureTimeUtc is null) return false;
        if (!DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", out var date)) return false;
        if (!TimeOnly.TryParseExact(departureTimeUtc, "HH:mm", out var time)) return false;
        return date.ToDateTime(time, DateTimeKind.Utc) > DateTime.UtcNow.AddMinutes(CheckInCutoffMinutes);
    }

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
}

// ─── Internal replacement model ────────────────────────────────────────────────

internal sealed class ReplacementOption
{
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string? DepartureTimeUtc { get; init; }
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
