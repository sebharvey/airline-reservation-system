namespace ReservationSystem.Orchestration.Retail.Models.Responses;

/// <summary>
/// Full order response returned by the manage-booking retrieve endpoints.
/// Maps the Order MS orderData JSON blob to the shape consumed by the Angular web app.
/// </summary>
public sealed class ManagedOrderResponse
{
    public string OrderId { get; init; } = string.Empty;
    public string BookingReference { get; init; } = string.Empty;
    public string OrderStatus { get; init; } = string.Empty;
    public string BookingType { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int? TotalPointsAmount { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<ManagedPassenger> Passengers { get; init; } = [];
    public IReadOnlyList<ManagedFlightSegment> FlightSegments { get; init; } = [];
    public IReadOnlyList<ManagedOrderItem> OrderItems { get; init; } = [];
    public IReadOnlyList<ManagedPayment> Payments { get; init; } = [];
    public ManagedPointsRedemption? PointsRedemption { get; init; }
}

public sealed class ManagedPassenger
{
    public string PassengerId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string DateOfBirth { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string? LoyaltyNumber { get; init; }
    public ManagedPassengerContacts? Contacts { get; init; }
    public ManagedTravelDocument? TravelDocument { get; init; }
}

public sealed class ManagedPassengerContacts
{
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

public sealed class ManagedTravelDocument
{
    public string Type { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string IssuingCountry { get; init; } = string.Empty;
    public string ExpiryDate { get; init; } = string.Empty;
    public string Nationality { get; init; } = string.Empty;
}

public sealed class ManagedFlightSegment
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

public sealed class ManagedOrderItem
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
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
    public IReadOnlyList<ManagedETicket> ETickets { get; init; } = [];
    public IReadOnlyList<ManagedSeatAssignment> SeatAssignments { get; init; } = [];
    public int? AdditionalBags { get; init; }
    public int? FreeBagsIncluded { get; init; }
}

public sealed class ManagedETicket
{
    public string PassengerId { get; init; } = string.Empty;
    public string ETicketNumber { get; init; } = string.Empty;
}

public sealed class ManagedSeatAssignment
{
    public string PassengerId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
}

public sealed class ManagedPayment
{
    public string PaymentReference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string CardLast4 { get; init; } = string.Empty;
    public string CardType { get; init; } = string.Empty;
    public decimal AuthorisedAmount { get; init; }
    public decimal SettledAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? AuthorisedAt { get; init; }
    public string? SettledAt { get; init; }
}

public sealed class ManagedPointsRedemption
{
    public string RedemptionReference { get; init; } = string.Empty;
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int PointsRedeemed { get; init; }
    public string Status { get; init; } = string.Empty;
}
