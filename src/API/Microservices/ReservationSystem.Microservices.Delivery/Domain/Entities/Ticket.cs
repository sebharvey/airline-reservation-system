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
/// Core domain entity representing an electronic ticket issued for one passenger
/// on one flight segment. Maps to [delivery].[Ticket].
/// </summary>
public sealed class Ticket
{
    public Guid TicketId { get; private set; }

    /// <summary>
    /// Database-generated IDENTITY value — the numeric second part of the IATA e-ticket number.
    /// The full formatted number (e.g. <c>932-1000000001</c>) is assembled at the API layer
    /// by prepending the airline accounting code prefix.
    /// </summary>
    public long TicketNumber { get; private set; }

    public string BookingReference { get; private set; } = string.Empty;
    public string PassengerId { get; private set; } = string.Empty;
    public bool IsVoided { get; private set; }
    public DateTime? VoidedAt { get; private set; }
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
            TicketNumber = 0, // assigned by the database IDENTITY on INSERT; EF Core reads it back via SCOPE_IDENTITY()
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

    /// <summary>
    /// Sets status to <paramref name="newStatus"/> on every coupon whose origin matches
    /// <paramref name="departureAirport"/> and that is not already at that status.
    /// Returns the number of coupons updated.
    /// </summary>
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
            if (string.Equals(status, "C", StringComparison.OrdinalIgnoreCase)) continue;

            var flightNumber = coupon["marketing"]?["flightNumber"]?.GetValue<string>() ?? "";
            var destination = coupon["destination"]?.GetValue<string>() ?? "";

            coupon["status"] = "C";

            history.Add(new JsonObject
            {
                ["eventType"] = "CouponStatusUpdated",
                ["occurredAt"] = DateTime.UtcNow.ToString("o"),
                ["actor"] = actor,
                ["detail"] = $"Coupon status set to C for {flightNumber} {origin}-{destination}"
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
    /// Returns all coupons in <see cref="TicketData"/> that are checked-in (status "C")
    /// and depart from <paramref name="departureAirport"/>.
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
            if (!string.Equals(status, "C", StringComparison.OrdinalIgnoreCase)) continue;

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
    /// Updates the status of the coupon matching the given flight number, origin, and destination
    /// within <see cref="TicketData"/>, then appends an audit entry to <c>changeHistory</c>.
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
