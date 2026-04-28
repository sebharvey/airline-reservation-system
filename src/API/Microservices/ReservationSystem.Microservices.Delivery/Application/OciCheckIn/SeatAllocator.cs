namespace ReservationSystem.Microservices.Delivery.Application.OciCheckIn;

/// <summary>
/// Allocates seat numbers from a cabin's seat pool, avoiding already-taken seats.
/// Attempts to assign a group of passengers in consecutive columns within the same row
/// so travelling companions sit together. Falls back to individual sequential allocation
/// if no single row can accommodate the full group.
///
/// Cabin configuration (columns, row range) must be supplied by the caller from the
/// active seatmap — this class contains no hardcoded aircraft layout assumptions.
/// </summary>
internal static class SeatAllocator
{
    /// <summary>
    /// Allocates <paramref name="count"/> seats for a group travelling together.
    /// Prefers consecutive columns in the same row; falls back to individual sequential
    /// seats across rows if no single row can fit the entire group.
    /// </summary>
    /// <param name="columns">Left-to-right column letters for this cabin (e.g. ["A","B","C","D","E","F","G","H","K"]).</param>
    /// <param name="startRow">First row number for this cabin.</param>
    /// <param name="endRow">Last row number for this cabin (inclusive).</param>
    /// <param name="count">Number of seats to allocate.</param>
    /// <param name="takenSeats">Seats already assigned on this flight.</param>
    public static IReadOnlyList<string> AllocateGroupSeats(
        IReadOnlyList<string> columns, int startRow, int endRow,
        int count, IReadOnlyList<string> takenSeats)
    {
        var takenSet = new HashSet<string>(takenSeats, StringComparer.OrdinalIgnoreCase);

        // Try to find 'count' consecutive available columns in the same row.
        for (var row = startRow; row <= endRow; row++)
        {
            for (var startCol = 0; startCol <= columns.Count - count; startCol++)
            {
                var allFree = true;
                for (var k = 0; k < count && allFree; k++)
                {
                    if (takenSet.Contains($"{row}{columns[startCol + k]}"))
                        allFree = false;
                }

                if (allFree)
                    return Enumerable.Range(0, count)
                        .Select(k => $"{row}{columns[startCol + k]}")
                        .ToList()
                        .AsReadOnly();
            }
        }

        // Fallback: assign the best available individual seats in row/column order.
        var assigned = new List<string>();
        for (var row = startRow; row <= endRow && assigned.Count < count; row++)
        {
            foreach (var col in columns)
            {
                if (assigned.Count >= count) break;
                var seat = $"{row}{col}";
                if (!takenSet.Contains(seat))
                {
                    takenSet.Add(seat); // Reserve for subsequent iterations within this call.
                    assigned.Add(seat);
                }
            }
        }

        return assigned.AsReadOnly();
    }
}
