namespace ReservationSystem.Microservices.Delivery.Application.OciCheckIn;

/// <summary>
/// Allocates seat numbers from a cabin's seat pool, avoiding already-taken seats.
/// Attempts to assign a group of passengers in consecutive columns within the same row
/// so travelling companions sit together. Falls back to individual sequential allocation
/// if no single row can accommodate the full group.
/// </summary>
internal static class SeatAllocator
{
    // Column orders match left-to-right physical layout per aircraft family.
    // Business (J/F): 1-2-1 herringbone — four selectable columns per row.
    // Premium Economy (W): 2-3-2 — seven columns, two blocks either side of centre.
    // Economy (Y): 3-3-3 — nine columns, three blocks.
    private static readonly IReadOnlyList<string> BusinessColumns         = ["A", "D", "G", "K"];
    private static readonly IReadOnlyList<string> PremiumEconomyColumns   = ["A", "B", "D", "E", "F", "H", "K"];
    private static readonly IReadOnlyList<string> EconomyColumns          = ["A", "B", "C", "D", "E", "F", "G", "H", "K"];

    private static (IReadOnlyList<string> Columns, int StartRow, int EndRow) GetCabinConfig(string cabinCode) =>
        cabinCode.ToUpperInvariant() switch
        {
            "J" or "F" => (BusinessColumns, 1, 10),
            "W"        => (PremiumEconomyColumns, 20, 28),
            _          => (EconomyColumns, 35, 62)
        };

    /// <summary>
    /// Allocates <paramref name="count"/> seats for a group travelling together.
    /// Prefers consecutive columns in the same row; falls back to individual sequential
    /// seats across rows if no single row can fit the entire group.
    /// </summary>
    public static IReadOnlyList<string> AllocateGroupSeats(
        string cabinCode, int count, IReadOnlyList<string> takenSeats)
    {
        var (columns, startRow, endRow) = GetCabinConfig(cabinCode);
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
