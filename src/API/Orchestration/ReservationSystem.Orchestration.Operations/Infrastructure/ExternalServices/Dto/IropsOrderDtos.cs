using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class AffectedOrdersResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("orders")]
    public IReadOnlyList<AffectedOrderDto> Orders { get; init; } = [];
}

public sealed class AffectedOrderDto
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; init; }

    [JsonPropertyName("bookingReference")]
    public string BookingReference { get; init; } = string.Empty;

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = "Revenue"; // "Revenue" | "Reward"

    [JsonPropertyName("loyaltyNumber")]
    public string? LoyaltyNumber { get; init; }

    [JsonPropertyName("loyaltyTier")]
    public string? LoyaltyTier { get; init; } // "Platinum" | "Gold" | "Silver" | "Blue"

    [JsonPropertyName("bookingDate")]
    public DateTime BookingDate { get; init; }

    [JsonPropertyName("totalPaid")]
    public decimal TotalPaid { get; init; }

    [JsonPropertyName("totalPointsAmount")]
    public int TotalPointsAmount { get; init; }

    [JsonPropertyName("originalPaymentId")]
    public string? OriginalPaymentId { get; init; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; init; } = string.Empty;

    [JsonPropertyName("segment")]
    public AffectedOrderSegmentDto Segment { get; init; } = new();

    [JsonPropertyName("passengers")]
    public IReadOnlyList<AffectedOrderPassengerDto> Passengers { get; init; } = [];
}

public sealed class AffectedOrderSegmentDto
{
    [JsonPropertyName("segmentId")]
    public string SegmentId { get; init; } = string.Empty;

    [JsonPropertyName("inventoryId")]
    public Guid InventoryId { get; init; }

    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("cabinCode")]
    public string CabinCode { get; init; } = string.Empty;

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;
}

public sealed class AffectedOrderPassengerDto
{
    [JsonPropertyName("passengerId")]
    public string PassengerId { get; init; } = string.Empty;

    [JsonPropertyName("givenName")]
    public string GivenName { get; init; } = string.Empty;

    [JsonPropertyName("surname")]
    public string Surname { get; init; } = string.Empty;

    [JsonPropertyName("passengerType")]
    public string PassengerType { get; init; } = "ADT";

    [JsonPropertyName("eTicketNumbers")]
    public IReadOnlyList<string> ETicketNumbers { get; init; } = [];
}

public sealed class RebookOrderRequest
{
    [JsonPropertyName("cancelledSegmentId")]
    public string CancelledSegmentId { get; init; } = string.Empty;

    [JsonPropertyName("replacementOfferIds")]
    public IReadOnlyList<string> ReplacementOfferIds { get; init; } = [];

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "FlightCancellation";

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = "Revenue";

    [JsonPropertyName("fromFlightNumber")]
    public string FromFlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("fromDepartureDate")]
    public string FromDepartureDate { get; init; } = string.Empty;

    [JsonPropertyName("toFlights")]
    public IReadOnlyList<RebookToFlightDto> ToFlights { get; init; } = [];
}

public sealed class RebookToFlightDto
{
    [JsonPropertyName("flightNumber")]
    public string FlightNumber { get; init; } = string.Empty;

    [JsonPropertyName("departureDate")]
    public string DepartureDate { get; init; } = string.Empty;
}

public sealed class CancelOrderRequest
{
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "IROPS";

    [JsonPropertyName("cancellationFeeAmount")]
    public decimal CancellationFeeAmount { get; init; } = 0;

    [JsonPropertyName("refundableAmount")]
    public decimal RefundableAmount { get; init; }

    [JsonPropertyName("originalPaymentId")]
    public string? OriginalPaymentId { get; init; }

    [JsonPropertyName("bookingType")]
    public string BookingType { get; init; } = "Revenue";

    [JsonPropertyName("pointsReinstated")]
    public int PointsReinstated { get; init; }
}
