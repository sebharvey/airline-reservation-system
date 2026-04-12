namespace ReservationSystem.Simulator.Models;

// ── Search ─────────────────────────────────────────────────────────────────────

internal sealed record SearchSliceRequest(
    string Origin,
    string Destination,
    string DepartureDate,
    int PaxCount,
    string BookingType);

internal sealed record SearchSliceResponse(
    string SessionId,
    List<SearchFlight> Flights);

internal sealed record SearchFlight(
    string FlightNumber,
    List<SearchCabin> Cabins);

internal sealed record SearchCabin(
    List<SearchFareFamily> FareFamilies);

internal sealed record SearchFareFamily(
    SearchOffer Offer);

internal sealed record SearchOffer(
    string OfferId);

// ── Basket ─────────────────────────────────────────────────────────────────────

internal sealed record CreateBasketRequest(
    List<BasketSegment> Segments,
    string ChannelCode,
    string CurrencyCode,
    string BookingType);

internal sealed record BasketSegment(
    string OfferId,
    string SessionId);

internal sealed record CreateBasketResponse(
    string BasketId);

// ── Passengers ─────────────────────────────────────────────────────────────────

internal sealed record PassengerRequest(
    string PassengerId,
    string Type,
    string GivenName,
    string Surname,
    string Dob,
    string Gender,
    string? LoyaltyNumber,
    PassengerContacts Contacts,
    object? TravelDocument);

internal sealed record PassengerContacts(
    string Email,
    string Phone);

// ── Get basket ─────────────────────────────────────────────────────────────────

internal sealed record GetBasketResponse(
    string BasketId,
    GetBasketData BasketData);

internal sealed record GetBasketData(
    List<BasketFlightOffer> FlightOffers);

internal sealed record BasketFlightOffer(
    string OfferId,
    string BasketItemId,
    string InventoryId,
    string FlightNumber,
    string AircraftType,
    string CabinCode);

// ── Seatmap ────────────────────────────────────────────────────────────────────

internal sealed record GetSeatmapResponse(
    List<SeatmapCabin> Cabins);

internal sealed record SeatmapCabin(
    string CabinCode,
    List<SeatResult> Seats);

internal sealed record SeatResult(
    string SeatOfferId,
    string SeatNumber,
    string Position,
    string CabinCode,
    decimal Price,
    string Currency,
    string Availability);

// ── Seats ──────────────────────────────────────────────────────────────────────

internal sealed record SeatAssignment(
    string PassengerId,
    string SegmentId,
    string BasketItemRef,
    string SeatOfferId,
    string SeatNumber,
    string SeatPosition,
    string CabinCode,
    decimal Price,
    string Currency);

// ── Confirm ────────────────────────────────────────────────────────────────────

internal sealed record ConfirmBasketRequest(
    PaymentRequest Payment,
    object? LoyaltyPointsToRedeem);

internal sealed record PaymentRequest(
    string Method,
    string CardNumber,
    string ExpiryDate,
    string Cvv,
    string CardholderName);

internal sealed record ConfirmBasketResponse(
    string OrderId,
    string BookingReference,
    string Status);
