namespace ReservationSystem.Simulator.Models;

// ── Search ─────────────────────────────────────────────────────────────────────

internal sealed record SearchSliceRequest(
    string Origin,
    string Destination,
    string DepartureDate,
    int PaxCount,
    string BookingType);

internal sealed record SearchSliceResponse(
    List<SearchItinerary> Itineraries);

internal sealed record SearchItinerary(
    List<SearchLeg> Legs);

internal sealed record SearchLeg(
    string SessionId,
    string FlightNumber,
    string Origin,
    string Destination,
    string DepartureDate,
    string DepartureTime,
    string ArrivalTime,
    int ArrivalDayOffset,
    string AircraftType,
    List<SearchCabin> Cabins);

internal sealed record SearchCabin(
    string CabinCode,
    int AvailableSeats,
    decimal FromPrice,
    string Currency,
    List<SearchFareFamily> FareFamilies);

internal sealed record SearchFareFamily(
    string FareFamily,
    SearchOffer Offer);

internal sealed record SearchOffer(
    string OfferId,
    string FareBasisCode,
    decimal BasePrice,
    decimal Tax,
    decimal TotalPrice,
    string Currency,
    bool IsRefundable,
    bool IsChangeable);

// ── Basket ─────────────────────────────────────────────────────────────────────

internal sealed record CreateBasketRequest(
    List<BasketSegment> Segments,
    string ChannelCode,
    string Currency,
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
    List<object> Docs);

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

// ── SSRs ───────────────────────────────────────────────────────────────────────

internal sealed record SsrRequest(
    string SsrCode,
    string PassengerRef,
    string SegmentRef);

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
