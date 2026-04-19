namespace ReservationSystem.Simulator.Models;

// ── Search ─────────────────────────────────────────────────────────────────────

public sealed record SearchSliceRequest(
    string Origin,
    string Destination,
    string DepartureDate,
    int PaxCount,
    string BookingType);

public sealed record SearchSliceResponse(
    List<SearchItinerary> Itineraries);

public sealed record SearchItinerary(
    List<SearchLeg> Legs);

public sealed record SearchLeg(
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

public sealed record SearchCabin(
    string CabinCode,
    int AvailableSeats,
    decimal FromPrice,
    string Currency,
    List<SearchFareFamily> FareFamilies);

public sealed record SearchFareFamily(
    string FareFamily,
    SearchOffer Offer);

public sealed record SearchOffer(
    string OfferId,
    string FareBasisCode,
    decimal BasePrice,
    decimal Tax,
    decimal TotalPrice,
    string Currency,
    bool IsRefundable,
    bool IsChangeable);

// ── Basket ─────────────────────────────────────────────────────────────────────

public sealed record CreateBasketRequest(
    List<BasketSegment> Segments,
    string Currency,
    string BookingType);

public sealed record BasketSegment(
    string OfferId,
    string SessionId);

public sealed record CreateBasketResponse(
    string BasketId);

// ── Passengers ─────────────────────────────────────────────────────────────────

public sealed record PassengerRequest(
    string PassengerId,
    string Type,
    string GivenName,
    string Surname,
    string Dob,
    string Gender,
    string? LoyaltyNumber,
    PassengerContacts Contacts,
    List<object> Docs);

public sealed record PassengerContacts(
    string Email,
    string Phone);

// ── Get basket ─────────────────────────────────────────────────────────────────

public sealed record GetBasketResponse(
    string BasketId,
    GetBasketData BasketData);

public sealed record GetBasketData(
    List<BasketFlightOffer> FlightOffers);

public sealed record BasketFlightOffer(
    string OfferId,
    string BasketItemId,
    string InventoryId,
    string FlightNumber,
    string AircraftType,
    string CabinCode);

// ── Seatmap ────────────────────────────────────────────────────────────────────

public sealed record GetSeatmapResponse(
    List<SeatmapCabin> Cabins);

public sealed record SeatmapCabin(
    string CabinCode,
    List<SeatResult> Seats);

public sealed record SeatResult(
    string SeatOfferId,
    string SeatNumber,
    string Position,
    string CabinCode,
    decimal Price,
    decimal Tax,
    string Currency,
    string Availability);

// ── Seats ──────────────────────────────────────────────────────────────────────

public sealed record SeatAssignment(
    string PassengerId,
    string SegmentId,
    string BasketItemRef,
    string SeatOfferId,
    string SeatNumber,
    string SeatPosition,
    string CabinCode,
    decimal Price,
    decimal Tax,
    string Currency);

// ── SSRs ───────────────────────────────────────────────────────────────────────

public sealed record SsrRequest(
    string SsrCode,
    string PassengerRef,
    string SegmentRef);

// ── Confirm ────────────────────────────────────────────────────────────────────

public sealed record ConfirmBasketRequest(
    string ChannelCode,
    PaymentRequest Payment,
    object? LoyaltyPointsToRedeem);

public sealed record PaymentRequest(
    string Method,
    string CardNumber,
    string ExpiryDate,
    string Cvv,
    string CardholderName);

public sealed record ConfirmBasketResponse(
    string OrderId,
    string BookingReference,
    string Status);
