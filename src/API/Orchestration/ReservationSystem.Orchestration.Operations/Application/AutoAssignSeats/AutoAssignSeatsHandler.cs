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

        // Passengers with no available seat — record immediately, no I/O needed
        var results = new List<SeatAssignmentOutcome>(planned.Count);
        foreach (var plan in planned.Where(p => p.SeatNumber is null))
        {
            results.Add(new SeatAssignmentOutcome(
                plan.BookingReference, plan.ETicketNumber, null, "Failed",
                plan.FailureReason ?? "No seats available in any cabin"));
        }

        // Persist all manifest seat assignments in parallel — each targets a different e-ticket
        var seatPlans      = planned.Where(p => p.SeatNumber is not null).ToList();
        var manifestTasks  = seatPlans.Select(p => PersistManifestSeatAsync(p, command.InventoryId, ct)).ToList();
        var manifestOutcomes = await Task.WhenAll(manifestTasks);

        // Evaluate manifest results; collect successful seats grouped by booking for batched order updates
        var seatsByBooking = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < seatPlans.Count; i++)
        {
            var plan    = seatPlans[i];
            var outcome = manifestOutcomes[i];

            if (outcome.Exception is not null)
            {
                _logger.LogError(outcome.Exception,
                    "AutoAssign: unexpected failure for e-ticket {ETicket}", plan.ETicketNumber);
                results.Add(new SeatAssignmentOutcome(
                    plan.BookingReference, plan.ETicketNumber, null, "Failed", outcome.Exception.Message));
                continue;
            }

            if (!outcome.Updated)
            {
                _logger.LogWarning(
                    "AutoAssign: manifest entry not found for e-ticket {ETicket}", plan.ETicketNumber);
                results.Add(new SeatAssignmentOutcome(
                    plan.BookingReference, plan.ETicketNumber, null, "Failed", "Manifest entry not found"));
                continue;
            }

            if (plan.AllocatedCabin is not null &&
                !string.Equals(plan.AllocatedCabin, plan.BookedCabin, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "AutoAssign: seat {Seat} ({AllocatedCabin}) assigned to e-ticket {ETicket} on booking {BookingRef} — overflow from booked cabin {BookedCabin}",
                    plan.SeatNumber, plan.AllocatedCabin, plan.ETicketNumber, plan.BookingReference, plan.BookedCabin);
            }
            else
            {
                _logger.LogInformation(
                    "AutoAssign: seat {Seat} assigned to e-ticket {ETicket} on booking {BookingRef}",
                    plan.SeatNumber, plan.ETicketNumber, plan.BookingReference);
            }

            results.Add(new SeatAssignmentOutcome(
                plan.BookingReference, plan.ETicketNumber, plan.SeatNumber, "Assigned", null));

            if (!seatsByBooking.TryGetValue(plan.BookingReference, out var bookingSeats))
                seatsByBooking[plan.BookingReference] = bookingSeats = [];

            bookingSeats.Add(new
            {
                passengerId = plan.PassengerId,
                segmentId   = command.InventoryId.ToString(),
                seatNumber  = plan.SeatNumber,
                price       = 0m,
                tax         = 0m,
                currency    = "GBP"
            });
        }

        // Update orders in parallel — one batched call per booking reference (best-effort)
        await Task.WhenAll(seatsByBooking.Select(kvp => UpdateOrderSeatsAsync(kvp.Key, kvp.Value, ct)));

        var assigned = results.Count(r => r.Status == "Assigned");
        var failed   = results.Count - assigned;
        return new AutoAssignSeatsResult(assigned, failed, results);
    }

    // ── Persistence helpers ──────────────────────────────────────────────────

    private async Task<(bool Updated, Exception? Exception)> PersistManifestSeatAsync(
        PlannedAssignment plan, Guid inventoryId, CancellationToken ct)
    {
        try
        {
            var updated = await _deliveryService.UpdateManifestSeatAsync(
                plan.ETicketNumber, inventoryId, plan.SeatNumber, ct);
            return (updated, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    private async Task UpdateOrderSeatsAsync(string bookingReference, List<object> seats, CancellationToken ct)
    {
        try
        {
            await _orderService.UpdateOrderSeatsPostSaleAsync(bookingReference, seats, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AutoAssign: order seat update failed for {BookingRef} — manifests were updated",
                bookingReference);
        }
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

    // Cabin downgrade order: when a cabin is full, try the next cabin down the chain.
    // Higher-value cabins are processed first so they get first pick of overflow space.
    private static readonly string[] CabinDowngradeChain = ["F", "J", "W", "Y"];

    private static List<PlannedAssignment> PlanAssignments(
        IReadOnlyList<ManifestEntryDto> unassigned,
        Dictionary<string, List<SeatSlot>> availablePerCabin)
    {
        var assignments = new List<PlannedAssignment>();
        var rng = new Random();
        var usedSeats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Index available seats by cabin → row for efficient lookup
        var cabinSeatRows = availablePerCabin.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .GroupBy(s => s.RowNumber)
                .ToDictionary(g => g.Key, g => g.ToList()),
            StringComparer.OrdinalIgnoreCase);

        // Process cabins in priority order so higher-value pax get first pick of overflow space
        foreach (var primaryCabin in CabinDowngradeChain)
        {
            var cabinPassengers = unassigned
                .Where(p => string.Equals(p.CabinCode, primaryCabin, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (cabinPassengers.Count == 0) continue;

            // Shuffle booking groups for random distribution within the cabin
            var bookingGroups = cabinPassengers
                .GroupBy(p => p.BookingReference, StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => rng.Next())
                .ToList();

            // Passengers who couldn't be seated in their primary cabin
            var overflowPassengers = new List<ManifestEntryDto>();

            foreach (var bookingGroup in bookingGroups)
            {
                var unseated = AllocateFromCabin(
                    bookingGroup.ToList(), primaryCabin, primaryCabin, cabinSeatRows, usedSeats, assignments, rng);
                overflowPassengers.AddRange(unseated);
            }

            if (overflowPassengers.Count == 0) continue;

            // Try overflow cabins in downgrade order, keeping booking groups together where possible
            var primaryCabinIdx = Array.IndexOf(CabinDowngradeChain, primaryCabin);

            var overflowGroups = overflowPassengers
                .GroupBy(p => p.BookingReference, StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => rng.Next())
                .ToList();

            foreach (var overflowGroup in overflowGroups)
            {
                var remaining = overflowGroup.ToList();

                for (var i = primaryCabinIdx + 1; i < CabinDowngradeChain.Length && remaining.Count > 0; i++)
                {
                    var overflowCabin = CabinDowngradeChain[i];
                    remaining = AllocateFromCabin(
                        remaining, primaryCabin, overflowCabin, cabinSeatRows, usedSeats, assignments, rng);
                }

                // Any still-remaining passengers could not be seated in any cabin
                foreach (var pax in remaining)
                {
                    assignments.Add(new PlannedAssignment(
                        pax.BookingReference, pax.ETicketNumber, pax.PassengerId,
                        SeatNumber: null, BookedCabin: primaryCabin, AllocatedCabin: null,
                        FailureReason: "No seats available in any cabin"));
                }
            }
        }

        return assignments;
    }

    /// <summary>
    /// Attempts to allocate seats from <paramref name="targetCabin"/> for the supplied passenger list.
    /// Prefers seating the whole group in one row; falls back to individual seats.
    /// Returns passengers that could not be seated (cabin exhausted).
    /// </summary>
    private static List<ManifestEntryDto> AllocateFromCabin(
        List<ManifestEntryDto> paxList,
        string bookedCabin,
        string targetCabin,
        Dictionary<string, Dictionary<int, List<SeatSlot>>> cabinSeatRows,
        HashSet<string> usedSeats,
        List<PlannedAssignment> assignments,
        Random rng)
    {
        if (!cabinSeatRows.TryGetValue(targetCabin, out var rows) || rows.Count == 0)
            return paxList; // No seats at all in this cabin

        var groupSize    = paxList.Count;
        var shuffledRows = rows.Keys.OrderBy(_ => rng.Next()).ToList();

        // Try to seat the whole group in one row
        if (groupSize > 1)
        {
            foreach (var rowNum in shuffledRows)
            {
                var freeInRow = rows[rowNum]
                    .Where(s => !usedSeats.Contains(s.SeatNumber))
                    .ToList();

                if (freeInRow.Count >= groupSize)
                {
                    for (var i = 0; i < groupSize; i++)
                    {
                        assignments.Add(new PlannedAssignment(
                            paxList[i].BookingReference, paxList[i].ETicketNumber, paxList[i].PassengerId,
                            SeatNumber: freeInRow[i].SeatNumber, BookedCabin: bookedCabin, AllocatedCabin: targetCabin));
                        usedSeats.Add(freeInRow[i].SeatNumber);
                    }
                    return []; // All placed
                }
            }
        }

        // Fallback: individual seats across rows in shuffled order
        var unseated      = new List<ManifestEntryDto>();
        var seatEnumerator = shuffledRows
            .SelectMany(r => rows[r].Where(s => !usedSeats.Contains(s.SeatNumber)))
            .GetEnumerator();

        foreach (var pax in paxList)
        {
            if (!seatEnumerator.MoveNext())
            {
                unseated.Add(pax);
                continue;
            }
            var seat = seatEnumerator.Current;
            assignments.Add(new PlannedAssignment(
                pax.BookingReference, pax.ETicketNumber, pax.PassengerId,
                SeatNumber: seat.SeatNumber, BookedCabin: bookedCabin, AllocatedCabin: targetCabin));
            usedSeats.Add(seat.SeatNumber);
        }

        return unseated;
    }

    // ── Internal value types ─────────────────────────────────────────────────

    private sealed record SeatSlot(string SeatNumber, int RowNumber, string Position, string CabinCode);

    private sealed record PlannedAssignment(
        string BookingReference,
        string ETicketNumber,
        int PassengerId,
        string? SeatNumber,
        string? BookedCabin = null,
        string? AllocatedCabin = null,
        string? FailureReason = null);
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
