namespace ReservationSystem.Orchestration.Retail.Application.OciPassengerDetails;

public sealed record OciPassengerDetailsCommand(
    string BookingReference,
    IReadOnlyList<OciPassengerDetailItem> Passengers);

public sealed record OciPassengerDetailItem(
    string PassengerId,
    OciTravelDocumentItem? TravelDocument);

public sealed record OciTravelDocumentItem(
    string Type,
    string Number,
    string IssuingCountry,
    string Nationality,
    string IssueDate,
    string ExpiryDate);
