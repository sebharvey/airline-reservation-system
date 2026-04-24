namespace ReservationSystem.Orchestration.Retail.Application.CheckInAncillaries;

public sealed record CheckInBagItem(
    string PassengerId,
    string SegmentId,
    string? BagOfferId,
    int AdditionalBags,
    decimal Price,
    string Currency);

public sealed record CheckInSeatItem(
    string PassengerId,
    string SegmentId,
    string SeatNumber,
    decimal SeatPrice,
    string Currency);

public sealed record CheckInAncillariesCommand(
    string BookingReference,
    Guid? BasketId,
    IReadOnlyList<CheckInBagItem> BagSelections,
    IReadOnlyList<CheckInSeatItem> SeatSelections,
    string? CardNumber,
    string? ExpiryDate,
    string? Cvv,
    string? CardholderName,
    string? CardLast4,
    string? CardType);
