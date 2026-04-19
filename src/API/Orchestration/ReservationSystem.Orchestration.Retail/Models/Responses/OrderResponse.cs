namespace ReservationSystem.Orchestration.Retail.Models.Responses;

public sealed class OrderResponse
{
    public Guid OrderId { get; init; }
    public string BookingReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string BookingType { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string BookedAt { get; init; } = string.Empty;

    // Pre-computed totals — the UI must display these as-is, never recalculate
    public decimal FareTotal { get; init; }
    public decimal SeatTotal { get; init; }
    public decimal BagTotal { get; init; }
    public decimal ProductTotal { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal? TotalPointsAmount { get; init; }

    public IReadOnlyList<ConfirmedPassenger> Passengers { get; init; } = [];
    public IReadOnlyList<ConfirmedFlightSegment> FlightSegments { get; init; } = [];
    public IReadOnlyList<ConfirmedOrderItem> OrderItems { get; init; } = [];
    public ConfirmedPayment? Payment { get; init; }
    public ConfirmedPointsRedemption? PointsRedemption { get; init; }
}

public sealed class ConfirmedPassenger
{
    public string PassengerId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string? Dob { get; init; }
    public string? Gender { get; init; }
    public string? LoyaltyNumber { get; init; }
    public ConfirmedPassengerContacts? Contacts { get; init; }
    public IReadOnlyList<ConfirmedTravelDoc> Docs { get; init; } = [];
}

public sealed class ConfirmedPassengerContacts
{
    public string? Email { get; init; }
    public string? Phone { get; init; }
}

public sealed class ConfirmedTravelDoc
{
    public string Type { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string IssuingCountry { get; init; } = string.Empty;
    public string ExpiryDate { get; init; } = string.Empty;
    public string Nationality { get; init; } = string.Empty;
}

public sealed class ConfirmedFlightSegment
{
    public string SegmentId { get; init; } = string.Empty;
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDateTime { get; init; } = string.Empty;
    public string ArrivalDateTime { get; init; } = string.Empty;
    public string AircraftType { get; init; } = string.Empty;
    public string OperatingCarrier { get; init; } = string.Empty;
    public string MarketingCarrier { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string BookingClass { get; init; } = string.Empty;
}

public sealed class ConfirmedOrderItem
{
    public string OrderItemId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string SegmentRef { get; init; } = string.Empty;
    public IReadOnlyList<string> PassengerRefs { get; init; } = [];
    public string? FareFamily { get; init; }
    public string? FareBasisCode { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Taxes { get; init; }
    public decimal TotalPrice { get; init; }
    public bool? IsRefundable { get; init; }
    public bool? IsChangeable { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public IReadOnlyList<ConfirmedETicket>? ETickets { get; init; }
    public string? SeatNumber { get; init; }
    public string? SeatPosition { get; init; }
    public int? AdditionalBags { get; init; }
    public string? ProductName { get; init; }
    public string? ProductOfferId { get; init; }
    public string? SsrCode { get; init; }
}

public sealed class ConfirmedETicket
{
    public string PassengerId { get; init; } = string.Empty;
    public string ETicketNumber { get; init; } = string.Empty;
}

public sealed class ConfirmedPayment
{
    public string PaymentReference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string CardLast4 { get; init; } = string.Empty;
    public string CardType { get; init; } = string.Empty;
    public string? CardholderName { get; init; }
    public string? MaskedCardNumber { get; init; }
    public decimal AuthorisedAmount { get; init; }
    public decimal SettledAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = "Settled";
    public string AuthorisedAt { get; init; } = string.Empty;
    public string SettledAt { get; init; } = string.Empty;
}

public sealed class ConfirmedPointsRedemption
{
    public string RedemptionReference { get; init; } = string.Empty;
    public string LoyaltyNumber { get; init; } = string.Empty;
    public decimal PointsRedeemed { get; init; }
    public string Status { get; init; } = "Settled";
    public string AuthorisedAt { get; init; } = string.Empty;
    public string SettledAt { get; init; } = string.Empty;
}

// Kept for internal use only — IssuedETicket is returned by the Delivery MS
public sealed class IssuedETicket
{
    public string PassengerId { get; init; } = string.Empty;
    public List<string> SegmentIds { get; init; } = [];
    public string ETicketNumber { get; init; } = string.Empty;
}
