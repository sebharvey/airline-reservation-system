using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Retail.Application.AdminManifest;

/// <summary>
/// Reconstructs flight-manifest entries from confirmed orders.
///
/// The authoritative passenger manifest lives in delivery.Manifest, written at booking
/// confirmation. Bookings confirmed before that write path existed (or before the paxId
/// fix that made it populate) have no manifest rows, so the flight-management page shows
/// zero passengers even though confirmed orders exist. This projection rebuilds the same
/// passenger view from the order record so those flights are not shown as empty. It is a
/// read-only fallback — used only when delivery.Manifest returns nothing for the flight —
/// and never overrides authoritative manifest data.
/// </summary>
public static class ManifestReconstruction
{
    public static List<AdminManifestEntry> FromOrders(
        IEnumerable<OrderMsOrderResult> orders,
        string flightNumber,
        string departureDate)
    {
        var entries = new List<AdminManifestEntry>();

        foreach (var order in orders)
        {
            if (order.OrderData is not JsonElement root || root.ValueKind != JsonValueKind.Object)
                continue;

            try
            {
                var orderEntries = ProjectOrder(order.OrderId, order.BookingReference ?? string.Empty, root, flightNumber, departureDate);
                entries.AddRange(orderEntries);
            }
            catch
            {
                // Skip any order whose data cannot be projected; a partial manifest is
                // better than failing the whole request.
            }
        }

        return entries;
    }

    private static IEnumerable<AdminManifestEntry> ProjectOrder(
        Guid orderId, string bookingReference, JsonElement root, string flightNumber, string departureDate)
    {
        var bookingType = root.TryGetProperty("bookingType", out var bt) && bt.ValueKind == JsonValueKind.String
            ? bt.GetString() ?? "Confirmed"
            : "Confirmed";

        // Locate the FLIGHT order item for this flight to source the cabin and inventory id.
        string cabinCode = string.Empty;
        string? flightInventoryId = null;
        var flightFound = false;

        if (root.TryGetProperty("orderItems", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (!IsProductType(item, "FLIGHT")) continue;

                var fn = GetString(item, "flightNumber");
                var dd = GetString(item, "departureDate");
                if (!string.Equals(fn, flightNumber, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(dd, departureDate, StringComparison.OrdinalIgnoreCase))
                    continue;

                cabinCode = GetString(item, "cabinCode") ?? string.Empty;
                flightInventoryId = GetString(item, "inventoryId");
                flightFound = true;
                break;
            }
        }

        // The order is not on this flight — contribute nothing.
        if (!flightFound)
            yield break;

        var eTicketByPax = BuildETicketMap(root);
        var seatByPax = BuildSeatMap(root, flightInventoryId);

        if (!root.TryGetProperty("dataLists", out var dataLists) ||
            dataLists.ValueKind != JsonValueKind.Object ||
            !dataLists.TryGetProperty("passengers", out var passengers) ||
            passengers.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var pax in passengers.EnumerateArray())
        {
            var passengerRef = GetString(pax, "passengerId");
            if (string.IsNullOrWhiteSpace(passengerRef))
                continue;

            var paxId = ParsePaxId(passengerRef);

            eTicketByPax.TryGetValue(passengerRef, out var eTicketNumber);
            seatByPax.TryGetValue(passengerRef, out var seatNumber);

            yield return new AdminManifestEntry
            {
                OrderId          = orderId,
                BookingReference = bookingReference,
                PassengerId      = paxId,
                GivenName        = GetString(pax, "givenName") ?? string.Empty,
                Surname          = GetString(pax, "surname") ?? string.Empty,
                ETicketNumber    = eTicketNumber ?? string.Empty,
                SeatNumber       = string.IsNullOrEmpty(seatNumber) ? null : seatNumber,
                CabinCode        = cabinCode,
                BookingType      = bookingType,
                CheckedIn        = false,
                SsrCodes         = [],
                Gender           = NormaliseGender(GetString(pax, "gender")),
                DateOfBirth      = ParseDate(GetString(pax, "dob")),
                PtcCode          = GetString(pax, "type") ?? "ADT"
            };
        }
    }

    private static Dictionary<string, string> BuildETicketMap(JsonElement root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("eTickets", out var eTickets) || eTickets.ValueKind != JsonValueKind.Array)
            return map;

        foreach (var et in eTickets.EnumerateArray())
        {
            var paxRef = GetString(et, "passengerId");
            var number = GetString(et, "eTicketNumber");
            if (!string.IsNullOrWhiteSpace(paxRef) && !string.IsNullOrWhiteSpace(number))
                map[paxRef!] = number!;
        }

        return map;
    }

    private static Dictionary<string, string> BuildSeatMap(JsonElement root, string? flightInventoryId)
    {
        // Prefer a seat whose segment matches the flight; fall back to any seat for the passenger.
        var preferred = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("orderItems", out var items) || items.ValueKind != JsonValueKind.Array)
            return preferred;

        foreach (var item in items.EnumerateArray())
        {
            if (!IsProductType(item, "SEAT")) continue;

            var paxRef  = GetString(item, "passengerId");
            var seatNum = GetString(item, "seatNumber");
            if (string.IsNullOrWhiteSpace(paxRef) || string.IsNullOrWhiteSpace(seatNum))
                continue;

            var segRef = GetString(item, "inventoryId") ?? GetString(item, "segmentId");
            if (flightInventoryId != null && string.Equals(segRef, flightInventoryId, StringComparison.OrdinalIgnoreCase))
                preferred[paxRef!] = seatNum!;
            else
                fallback.TryAdd(paxRef!, seatNum!);
        }

        foreach (var kv in fallback)
            preferred.TryAdd(kv.Key, kv.Value);

        return preferred;
    }

    private static bool IsProductType(JsonElement item, string productType) =>
        item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty("productType", out var pt) &&
        pt.ValueKind == JsonValueKind.String &&
        string.Equals(pt.GetString(), productType, StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement obj, string property) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(property, out var el) &&
        el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    // Passenger references are the PAX-N form (e.g. "PAX-1"); tolerate a bare integer too.
    private static int ParsePaxId(string passengerRef)
    {
        var raw = passengerRef.StartsWith("PAX-", StringComparison.OrdinalIgnoreCase)
            ? passengerRef[4..]
            : passengerRef;
        return int.TryParse(raw, out var n) ? n : 0;
    }

    private static string? NormaliseGender(string? gender) => gender?.Trim().ToUpperInvariant() switch
    {
        "M" or "MALE"   => "M",
        "F" or "FEMALE" => "F",
        null or ""      => null,
        _               => "U"
    };

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, out var d) ? d : null;
}
