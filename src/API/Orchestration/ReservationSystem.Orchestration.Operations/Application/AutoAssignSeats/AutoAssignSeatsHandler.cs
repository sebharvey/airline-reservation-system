using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

namespace ReservationSystem.Orchestration.Operations.Application.AutoAssignSeats;

public sealed class AutoAssignSeatsHandler
{
    private readonly DeliveryServiceClient _deliveryService;
    private readonly SeatServiceClient _seatService;
    private readonly OrderServiceClient _orderService;
    private readonly ILogger<AutoAssignSeatsHandler> _logger;

    public AutoAssignSeatsHandler(
        DeliveryServiceClient deliveryService,
        SeatServiceClient seatService,
        OrderServiceClient orderService,
        ILogger<AutoAssignSeatsHandler> logger)
    {
        _deliveryService = deliveryService;
        _seatService     = seatService;
        _orderService    = orderService;
        _logger          = logger;
    }

    public async Task<AutoAssignSeatsResult> HandleAsync(AutoAssignSeatsCommand command, CancellationToken ct)
    {
        // Fetch manifest and seatmap in parallel
        var manifestTask = _deliveryService.GetManifestAsync(command.FlightNumber, command.DepartureDate, ct);
        var seatmapTask  = _seatService.GetFullSeatmapAsync(command.AircraftType, ct);
        await Task.WhenAll(manifestTask, seatmapTask);

        var manifest = await manifestTask;
        var seatmap  = await seatmapTask;

        if (seatmap is null)
            throw new KeyNotFoundException($"No seatmap found for aircraft type '{command.AircraftType}'.");

        // Build the set of seats already occupied on the manifest
        var occupiedSeats = manifest.Entries
            .Where(e => !string.IsNullOrEmpty(e.SeatNumber))
            .Select(e => e.SeatNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Passengers needing seats: not standby, no seat yet
        var unassigned = manifest.Entries
            .Where(e => string.IsNullOrEmpty(e.SeatNumber) &&
                        !string.Equals(e.BookingType, "Standby", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (unassigned.Count == 0)
            return new AutoAssignSeatsResult(0, 0, []);

        // Build available seat pool per cabin code
        var availablePerCabin = BuildAvailableSeatsPerCabin(seatmap, occupiedSeats);

        // Compute seat assignments (pure logic, no I/O)
        var planned = PlanAssignments(unassigned, availablePerCabin);

        // Persist each assignment
        var results = new List<SeatAssignmentOutcome>(planned.Count);
        foreach (var plan in planned)
        {
            try
            {
                var updated = await _deliveryService.UpdateManifestSeatAsync(
                    plan.ETicketNumber, command.InventoryId, plan.SeatNumber, ct);

                if (!updated)
                {
                    _logger.LogWarning(
                        "AutoAssign: manifest entry not found for e-ticket {ETicket}", plan.ETicketNumber);
                    results.Add(new SeatAssignmentOutcome(
                        plan.BookingReference, plan.ETicketNumber, null, "Failed", "Manifest entry not found"));
                    continue;
                }

                // Update order seat — best-effort; manifest is the authoritative departure record
                try
                {
                    var seatsPayload = new[]
                    {
                        new
                        {
                            passengerId = plan.PassengerId,
                            segmentId   = command.InventoryId.ToString(),
                            seatNumber  = plan.SeatNumber,
                            price       = 0m,
                            tax         = 0m,
                            currency    = "GBP"
                        }
                    };
                    await _orderService.UpdateOrderSeatsPostSaleAsync(plan.BookingReference, seatsPayload, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "AutoAssign: order seat update failed for {BookingRef}/{ETicket} — manifest was updated",
                        plan.BookingReference, plan.ETicketNumber);
                }

                results.Add(new SeatAssignmentOutcome(
                    plan.BookingReference, plan.ETicketNumber, plan.SeatNumber, "Assigned", null));

                _logger.LogInformation(
                    "AutoAssign: seat {Seat} assigned to e-ticket {ETicket} on booking {BookingRef}",
                    plan.SeatNumber, plan.ETicketNumber, plan.BookingReference);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AutoAssign: unexpected failure for e-ticket {ETicket}", plan.ETicketNumber);
                results.Add(new SeatAssignmentOutcome(
                    plan.BookingReference, plan.ETicketNumber, null, "Failed", ex.Message));
            }
        }

        var assigned = results.Count(r => r.Status == "Assigned");
        var failed   = results.Count - assigned;
        return new AutoAssignSeatsResult(assigned, failed, results);
    }

    // ── Seat pool construction ───────────────────────────────────────────────

    private static Dictionary<string, List<SeatSlot>> BuildAvailableSeatsPerCabin(
        FullSeatmapDto seatmap, HashSet<string> occupiedSeats)
    {
        var result = new Dictionary<string, List<SeatSlot>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cabin in seatmap.Cabins)
        {
            var available = new List<SeatSlot>();
            foreach (var row in cabin.Rows)
            {
                foreach (var seat in row.Seats)
                {
                    if (!seat.IsSelectable) continue;
                    if (occupiedSeats.Contains(seat.SeatNumber)) continue;
                    available.Add(new SeatSlot(seat.SeatNumber, row.RowNumber, seat.Position, cabin.CabinCode));
                }
            }
            result[cabin.CabinCode] = available;
        }

        return result;
    }

    // ── Seat assignment algorithm ────────────────────────────────────────────

    private static List<PlannedAssignment> PlanAssignments(
        IReadOnlyList<ManifestEntryDto> unassigned,
        Dictionary<string, List<SeatSlot>> availablePerCabin)
    {
        var assignments = new List<PlannedAssignment>();
        var rng = new Random();

        // Process each cabin independently
        foreach (var cabinGroup in unassigned.GroupBy(p => p.CabinCode, StringComparer.OrdinalIgnoreCase))
        {
            var cabinCode = cabinGroup.Key;
            if (!availablePerCabin.TryGetValue(cabinCode, out var availableSeats) || availableSeats.Count == 0)
                continue;

            // Shuffle booking groups so distribution is random
            var bookingGroups = cabinGroup
                .GroupBy(p => p.BookingReference, StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => rng.Next())
                .ToList();

            // Index available seats by row and shuffle row order
            var seatsByRow = availableSeats
                .GroupBy(s => s.RowNumber)
                .ToDictionary(g => g.Key, g => g.ToList());
            var shuffledRows = seatsByRow.Keys.OrderBy(_ => rng.Next()).ToList();

            var usedSeats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in bookingGroups)
            {
                var paxList   = group.ToList();
                var groupSize = paxList.Count;
                var placed    = false;

                if (groupSize > 1)
                {
                    // Try to seat the whole booking in one row
                    foreach (var rowNum in shuffledRows)
                    {
                        var freeInRow = seatsByRow[rowNum]
                            .Where(s => !usedSeats.Contains(s.SeatNumber))
                            .ToList();

                        if (freeInRow.Count >= groupSize)
                        {
                            for (var i = 0; i < groupSize; i++)
                            {
                                var seat = freeInRow[i];
                                var pax  = paxList[i];
                                assignments.Add(new PlannedAssignment(
                                    pax.BookingReference, pax.ETicketNumber, pax.PassengerId, seat.SeatNumber));
                                usedSeats.Add(seat.SeatNumber);
                            }
                            placed = true;
                            break;
                        }
                    }
                }

                if (placed) continue;

                // Fallback: assign each pax individually to the next free seat
                // Iterate seats in shuffled row order so pax are still spread randomly
                var remaining = shuffledRows
                    .SelectMany(r => seatsByRow[r].Where(s => !usedSeats.Contains(s.SeatNumber)))
                    .GetEnumerator();

                foreach (var pax in paxList)
                {
                    if (!remaining.MoveNext()) break;
                    var seat = remaining.Current;
                    assignments.Add(new PlannedAssignment(
                        pax.BookingReference, pax.ETicketNumber, pax.PassengerId, seat.SeatNumber));
                    usedSeats.Add(seat.SeatNumber);
                }
            }
        }

        return assignments;
    }

    // ── Internal value types ─────────────────────────────────────────────────

    private sealed record SeatSlot(string SeatNumber, int RowNumber, string Position, string CabinCode);

    private sealed record PlannedAssignment(
        string BookingReference,
        string ETicketNumber,
        int PassengerId,
        string SeatNumber);
}

// ── Public result types ──────────────────────────────────────────────────────

public sealed record AutoAssignSeatsResult(
    int Assigned,
    int Failed,
    IReadOnlyList<SeatAssignmentOutcome> Outcomes);

public sealed record SeatAssignmentOutcome(
    string BookingReference,
    string ETicketNumber,
    string? SeatNumber,
    string Status,
    string? FailureReason);
