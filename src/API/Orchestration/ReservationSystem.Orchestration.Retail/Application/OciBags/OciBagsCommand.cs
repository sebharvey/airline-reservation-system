namespace ReservationSystem.Orchestration.Retail.Application.OciBags;

public sealed record OciBagsCommand(
    string BookingReference,
    IReadOnlyList<OciBagItemCommand> BagSelections,
    OciPaymentCommand? Payment);

public sealed record OciBagItemCommand(
    string PassengerId,
    string SegmentRef,
    string BagOfferId,
    int AdditionalBags);

public sealed record OciPaymentCommand(
    string Method,
    string CardNumber,
    string ExpiryDate,
    string Cvv,
    string CardholderName);
