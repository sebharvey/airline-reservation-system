using System.Text.Json.Nodes;

namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>Snapshot of a single coupon read from TicketData JSON.</summary>
public sealed record CouponInfo(
    string FlightNumber,
    string Origin,
    string Destination,
    string DepartureDate,
    string ClassOfService,
    string? SeatNumber,
    string Status);

/// <summary>
/// Aggregate root for an issued e-ticket. One ticket covers one passenger across all flight
/// segments in the booking. Each segment is represented by a coupon stored in TicketData.coupons.
///
/// All financial data (baseFare, currency, fareCalculationLine, taxes with coupon attribution)
/// lives in <c>TicketData.fareConstruction</c>. Coupon-level value is always <em>derived</em>
/// — never stored directly.
/// </summary>
public sealed class Ticket
{
    public Guid TicketId { get; private set; }

    /// <summary>
    /// Database-generated IDENTITY value — the numeric second part of the IATA e-ticket number.
    /// The full formatted number (e.g. <c>932-1000000001</c>) is assembled at the API layer.
    /// </summary>
    public long TicketNumber { get; private set; }

    public string BookingReference { get; private set; } = string.Empty;
    public string PassengerId { get; private set; } = string.Empty;

    public bool IsVoided { get; private set; }
    public DateTime? VoidedAt { get; private set; }

    /// <summary>
    /// Passenger info, fareConstruction (baseFare, currency, taxes with coupon attribution),
    /// coupons, form of payment, commission, endorsements, change history.
    /// </summary>
    public string TicketData { get; private set; } = "{}";

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int Version { get; private set; }

    private Ticket() { }

    public static Ticket Create(
        string bookingReference,
        string passengerId,
        string ticketData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        var now = DateTime.UtcNow;
        return new Ticket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = 0, // assigned by the database IDENTITY on INSERT
            BookingReference = bookingReference,
            PassengerId = passengerId,
            IsVoided = false,
            VoidedAt = null,
            TicketData = ticketData,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
    }

    public static Ticket Reconstitute(
        Guid ticketId, long ticketNumber, string bookingReference,
        string passengerId, bool isVoided, DateTime? voidedAt,
        string ticketData, DateTime createdAt, DateTime updatedAt, int version)
    {
        return new Ticket
        {
            TicketId = ticketId,
            TicketNumber = ticketNumber,
            BookingReference = bookingReference,
            PassengerId = passengerId,
            IsVoided = isVoided,
            VoidedAt = voidedAt,
            TicketData = ticketData,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Version = version
        };
    }

    public void Void()
    {
        IsVoided = true;
        VoidedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Coupon status mutations (TicketData JSON) ───────────────────────────────

    /// <summary>Sets status to CheckedIn on every coupon departing from <paramref name="departureAirport"/>. Returns count updated.</summary>
    public int CheckInCouponsForOrigin(string departureAirport, string actor)
    {
        var root = JsonNode.Parse(TicketData)?.AsObject();
        if (root is null) return 0;

        var coupons = root["coupons"]?.AsArray();
        if (coupons is null) return 0;

        var history = root["changeHistory"]?.AsArray() ?? new JsonArray();
        root["changeHistory"] = history;

        var updatedCount = 0;
        foreach (var node in coupons)
        {
            if (node is not JsonObject coupon) continue;

            var origin = coupon["origin"]?.GetValue<string>() ?? "";
            var status = coupon["status"]?.GetValue<string>() ?? "";

            if (!string.Equals(origin, departureAirport, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(status, CouponStatus.CheckedIn, StringComparison.OrdinalIgnoreCase)) continue;

            var flightNumber = coupon["marketing"]?["flightNumber"]?.GetValue<string>() ?? "";
            var destination = coupon["destination"]?.GetValue<string>() ?? "";

            coupon["status"] = CouponStatus.CheckedIn;

            history.Add(new JsonObject
            {
                ["eventType"] = "CouponStatusUpdated",
                ["occurredAt"] = DateTime.UtcNow.ToString("o"),
                ["actor"] = actor,
                ["detail"] = $"Coupon status set to {CouponStatus.CheckedIn} for {flightNumber} {origin}-{destination}"
            });

            updatedCount++;
        }

        if (updatedCount > 0)
        {
            TicketData = root.ToJsonString();
            UpdatedAt = DateTime.UtcNow;
        }

        return updatedCount;
    }

    /// <summary>
    /// Returns the first coupon departing from <paramref name="departureAirport"/> regardless of status.
    /// Returns <c>null</c> if no matching coupon exists.
    /// </summary>
    public CouponInfo? GetCouponForOrigin(string departureAirport)
    {
        var root = JsonNode.Parse(TicketData)?.AsObject();
        if (root is null) return null;

        var coupons = root["coupons"]?.AsArray();
        if (coupons is null) return null;

        foreach (var node in coupons)
        {
            if (node is not JsonObject coupon) continue;
            var origin = coupon["origin"]?.GetValue<string>() ?? "";
            if (!string.Equals(origin, departureAirport, StringComparison.OrdinalIgnoreCase)) continue;

            return new CouponInfo(
                FlightNumber: coupon["marketing"]?["flightNumber"]?.GetValue<string>() ?? "",
                Origin: origin,
                Destination: coupon["destination"]?.GetValue<string>() ?? "",
                DepartureDate: coupon["departureDate"]?.GetValue<string>() ?? "",
                ClassOfService: coupon["classOfService"]?.GetValue<string>() ?? "Y",
                SeatNumber: coupon["seat"]?.GetValue<string>(),
                Status: coupon["status"]?.GetValue<string>() ?? "");
        }

        return null;
    }

    /// <summary>
    /// Returns all coupons checked in (status CHECKED_IN) departing from <paramref name="departureAirport"/>.
    /// </summary>
    public IReadOnlyList<CouponInfo> GetCheckedInCouponsForOrigin(string departureAirport)
    {
        var root = JsonNode.Parse(TicketData)?.AsObject();
        if (root is null) return [];

        var coupons = root["coupons"]?.AsArray();
        if (coupons is null) return [];

        var result = new List<CouponInfo>();
        foreach (var node in coupons)
        {
            if (node is not JsonObject coupon) continue;

            var origin = coupon["origin"]?.GetValue<string>() ?? "";
            var status = coupon["status"]?.GetValue<string>() ?? "";

            if (!string.Equals(origin, departureAirport, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(status, CouponStatus.CheckedIn, StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(new CouponInfo(
                FlightNumber: coupon["marketing"]?["flightNumber"]?.GetValue<string>() ?? "",
                Origin: origin,
                Destination: coupon["destination"]?.GetValue<string>() ?? "",
                DepartureDate: coupon["departureDate"]?.GetValue<string>() ?? "",
                ClassOfService: coupon["classOfService"]?.GetValue<string>() ?? "Y",
                SeatNumber: coupon["seat"]?.GetValue<string>(),
                Status: status));
        }
        return result.AsReadOnly();
    }

    /// <summary>Returns the passenger name stored in TicketData.</summary>
    public (string GivenName, string Surname) GetPassengerName()
    {
        var root = JsonNode.Parse(TicketData)?.AsObject();
        var pax = root?["passenger"]?.AsObject();
        return (
            pax?["givenName"]?.GetValue<string>() ?? "",
            pax?["surname"]?.GetValue<string>() ?? "");
    }

    /// <summary>
    /// Assigns <paramref name="seatNumber"/> to every coupon departing from
    /// <paramref name="departureAirport"/> that does not already have a seat recorded.
    /// Returns <c>true</c> if at least one coupon was updated.
    /// </summary>
    public bool AssignSeatForOrigin(string departureAirport, string seatNumber, string actor)
    {
        var root = JsonNode.Parse(TicketData)?.AsObject();
        if (root is null) return false;

        var coupons = root["coupons"]?.AsArray();
        if (coupons is null) return false;

        var history = root["changeHistory"]?.AsArray() ?? new JsonArray();
        root["changeHistory"] = history;

        var updated = false;
        foreach (var node in coupons)
        {
            if (node is not JsonObject coupon) continue;

            var origin = coupon["origin"]?.GetValue<string>() ?? "";
            if (!string.Equals(origin, departureAirport, StringComparison.OrdinalIgnoreCase)) continue;

            var existingSeat = coupon["seat"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(existingSeat)) continue;

            coupon["seat"] = seatNumber;
            updated = true;

            var flightNumber = coupon["marketing"]?["flightNumber"]?.GetValue<string>() ?? "";
            var destination = coupon["destination"]?.GetValue<string>() ?? "";
            history.Add(new JsonObject
            {
                ["eventType"] = "SeatAssigned",
                ["occurredAt"] = DateTime.UtcNow.ToString("o"),
                ["actor"] = actor,
                ["detail"] = $"Seat {seatNumber} auto-assigned at OLCI for {flightNumber} {origin}-{destination}"
            });
        }

        if (updated)
        {
            TicketData = root.ToJsonString();
            UpdatedAt = DateTime.UtcNow;
        }

        return updated;
    }

    /// <summary>
    /// Updates the status of the coupon matching the given flight number, origin, and destination.
    /// Returns <c>true</c> if a matching coupon was found and updated.
    /// </summary>
    public bool UpdateCouponStatus(string flightNumber, string origin, string destination, string newStatus, string actor)
    {
        var root = JsonNode.Parse(TicketData)?.AsObject();
        if (root is null) return false;

        var coupons = root["coupons"]?.AsArray();
        if (coupons is null) return false;

        JsonObject? matched = null;
        foreach (var node in coupons)
        {
            if (node is not JsonObject coupon) continue;

            var couponFlight = coupon["marketing"]?["flightNumber"]?.GetValue<string>();
            var couponOrigin = coupon["origin"]?.GetValue<string>();
            var couponDest = coupon["destination"]?.GetValue<string>();

            if (string.Equals(couponFlight, flightNumber, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(couponOrigin, origin, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(couponDest, destination, StringComparison.OrdinalIgnoreCase)))
            {
                matched = coupon;
                break;
            }
        }

        if (matched is null) return false;

        matched["status"] = newStatus;

        var history = root["changeHistory"]?.AsArray() ?? new JsonArray();
        root["changeHistory"] = history;
        history.Add(new JsonObject
        {
            ["eventType"] = "CouponStatusUpdated",
            ["occurredAt"] = DateTime.UtcNow.ToString("o"),
            ["actor"] = actor,
            ["detail"] = $"Coupon status set to {newStatus} for {flightNumber} {origin}-{destination}"
        });

        TicketData = root.ToJsonString();
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}

/// <summary>Canonical coupon status strings per IATA ticketing conventions.</summary>
public static class CouponStatus
{
    public const string Open        = "OPEN";
    public const string CheckedIn   = "CHECKED_IN";
    public const string Lifted      = "LIFTED";
    public const string Flown       = "FLOWN";
    public const string Refunded    = "REFUNDED";
    public const string Void        = "VOID";
    public const string Exchanged   = "EXCHANGED";
    public const string PrintExchange = "PRINT_EXCHANGE";
}
