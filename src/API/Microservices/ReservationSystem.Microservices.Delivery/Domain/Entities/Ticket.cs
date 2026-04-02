using System.Text.Json.Nodes;

namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Core domain entity representing an electronic ticket issued for one passenger
/// on one flight segment. Maps to [delivery].[Ticket].
/// </summary>
public sealed class Ticket
{
    public Guid TicketId { get; private set; }
    public string ETicketNumber { get; private set; } = string.Empty;
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
        string eTicketNumber,
        string bookingReference,
        string passengerId,
        string ticketData = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eTicketNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        var now = DateTime.UtcNow;
        return new Ticket
        {
            TicketId = Guid.NewGuid(),
            ETicketNumber = eTicketNumber,
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
        Guid ticketId, string eTicketNumber, string bookingReference,
        string passengerId, bool isVoided, DateTime? voidedAt,
        string ticketData, DateTime createdAt, DateTime updatedAt, int version)
    {
        return new Ticket
        {
            TicketId = ticketId,
            ETicketNumber = eTicketNumber,
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
