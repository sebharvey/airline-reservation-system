namespace ReservationSystem.Orchestration.Retail.Models.Responses;

/// <summary>
/// Full payment-screen summary derived from the basket.
/// All monetary amounts are pre-calculated by the API; the client displays as-is.
/// </summary>
public sealed class PaymentSummaryResponse
{
    public Guid BasketId { get; init; }
    public string BookingType { get; init; } = "Revenue";
    public string Currency { get; init; } = string.Empty;
    public string? TicketingTimeLimit { get; init; }
    public IReadOnlyList<PaymentSummaryFlight> Flights { get; init; } = [];
    public IReadOnlyList<PaymentSummaryPassenger> Passengers { get; init; } = [];
    public IReadOnlyList<PaymentSummarySeatSelection> SeatSelections { get; init; } = [];
    public IReadOnlyList<PaymentSummaryBagSelection> BagSelections { get; init; } = [];
    public IReadOnlyList<PaymentSummaryProductSelection> ProductSelections { get; init; } = [];
    public IReadOnlyList<PaymentSummarySsrSelection> SsrSelections { get; init; } = [];
    public PaymentSummaryTotals Totals { get; init; } = new();
}

public sealed class PaymentSummaryFlight
{
    public Guid OfferId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    /// <summary>ISO 8601 combined departure datetime.</summary>
    public string DepartureDateTime { get; init; } = string.Empty;
    /// <summary>ISO 8601 combined arrival datetime.</summary>
    public string ArrivalDateTime { get; init; } = string.Empty;
    public string CabinCode { get; init; } = string.Empty;
    public string CabinName { get; init; } = string.Empty;
    public string? FareFamily { get; init; }
    /// <summary>Base fare for all passengers on this flight.</summary>
    public decimal FareAmount { get; init; }
    /// <summary>Tax portion for all passengers on this flight.</summary>
    public decimal TaxAmount { get; init; }
    /// <summary>Total (fare + tax) for all passengers on this flight.</summary>
    public decimal TotalAmount { get; init; }
}

public sealed class PaymentSummaryPassenger
{
    public string PassengerId { get; init; } = string.Empty;
    /// <summary>IATA passenger type: ADT, CHD, INF, YTH.</summary>
    public string Type { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
}

public sealed class PaymentSummarySeatSelection
{
    public string PassengerId { get; init; } = string.Empty;
    public string SeatNumber { get; init; } = string.Empty;
    public string SeatPosition { get; init; } = string.Empty;
    /// <summary>Flight number of the associated flight offer.</summary>
    public string FlightNumber { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Tax { get; init; }
    public string Currency { get; init; } = string.Empty;
}

public sealed class PaymentSummaryBagSelection
{
    public string PassengerId { get; init; } = string.Empty;
    public int AdditionalBags { get; init; }
    /// <summary>Flight number of the associated flight offer.</summary>
    public string FlightNumber { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Tax { get; init; }
    public string Currency { get; init; } = string.Empty;
}

public sealed class PaymentSummaryProductSelection
{
    public string PassengerId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Tax { get; init; }
    public string Currency { get; init; } = string.Empty;
    /// <summary>References a basketItemId if the product is segment-specific; null for journey-wide products.</summary>
    public string? SegmentRef { get; init; }
}

public sealed class PaymentSummarySsrSelection
{
    public string SsrCode { get; init; } = string.Empty;
    public string PassengerId { get; init; } = string.Empty;
}

/// <summary>
/// All monetary totals pre-calculated server-side.
/// The Angular client renders these values directly — no arithmetic in the front end.
/// </summary>
public sealed class PaymentSummaryTotals
{
    /// <summary>Base fare for all passengers across all flights. Zero for reward bookings.</summary>
    public decimal FareAmount { get; init; }
    /// <summary>Tax portion for all passengers. For reward bookings this is the full cash amount owed.</summary>
    public decimal TaxAmount { get; init; }
    /// <summary>Total seat ancillary charges.</summary>
    public decimal SeatAmount { get; init; }
    /// <summary>Total bag ancillary charges.</summary>
    public decimal BagAmount { get; init; }
    /// <summary>Total product ancillary charges.</summary>
    public decimal ProductAmount { get; init; }
    /// <summary>Total loyalty points to be redeemed. Zero for revenue bookings.</summary>
    public int PointsAmount { get; init; }
    /// <summary>Total amount due in cash (card payment total).</summary>
    public decimal GrandTotal { get; init; }
}
