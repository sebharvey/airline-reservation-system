namespace ReservationSystem.Orchestration.Retail.Models.Responses;

// ── Unified slice search response types ──────────────────────────────────────
// Returned by POST /v1/search/slice.
// Both direct and connecting itineraries use this shape; direct flights have a
// single entry in Legs while connecting itineraries have two.

/// <summary>
/// Response from POST /v1/search/slice.
/// Contains one entry per bookable itinerary.  Direct flights have a single leg;
/// connecting flights (via LHR) have two.  The web client does not need to know
/// in advance whether a route is direct or connecting — the backend detects this
/// automatically and falls back to connecting search when no direct flight exists.
/// </summary>
public sealed class SliceSearchResponse
{
    public IReadOnlyList<SliceItinerary> Itineraries { get; init; } = [];
}

/// <summary>
/// One bookable itinerary — either a direct single-leg flight or a two-leg
/// connecting option via LHR.
/// </summary>
public sealed class SliceItinerary
{
    public IReadOnlyList<SliceLeg> Legs { get; init; } = [];
    /// <summary>
    /// Null for direct flights; layover minutes at LHR for connecting flights.
    /// </summary>
    public int? ConnectionDurationMinutes { get; init; }
    public decimal CombinedFromPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// One flight leg within an itinerary.  Each leg carries its own SessionId because
/// the Offer MS creates an independent StoredOffer per search; the client must pass
/// the correct SessionId alongside each OfferId when creating the basket.
/// </summary>
public sealed class SliceLeg
{
    public Guid SessionId { get; init; }
    public Guid InventoryId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public int DurationMinutes { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public IReadOnlyList<CabinSearchResult> Cabins { get; init; } = [];
}

// ── Connecting search response types ─────────────────────────────────────────

/// <summary>
/// Response from POST /v1/search/connecting.
/// Contains paired itinerary options assembled by the Retail API from two independent
/// Offer MS searches (one per leg), filtered to only include connections that meet
/// the 60-minute minimum connection time at LHR.
/// </summary>
public sealed class ConnectingSearchResponse
{
    public IReadOnlyList<ConnectingItinerary> Itineraries { get; init; } = [];
}

/// <summary>
/// A single connecting itinerary pairing a first leg (origin → LHR) with a second leg
/// (LHR → destination). ConnectionDurationMinutes is the layover at LHR.
/// Pass Leg1.Cabins[*].FareFamilies[*].Offer.OfferId with Leg1.SessionId, and the
/// equivalent Leg2 fields, as the two segments when creating the basket.
/// </summary>
public sealed class ConnectingItinerary
{
    public ConnectingLeg Leg1 { get; init; } = new();
    public ConnectingLeg Leg2 { get; init; } = new();
    public int ConnectionDurationMinutes { get; init; }
    public decimal CombinedFromPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// One leg of a connecting itinerary.
/// SessionId identifies the Offer MS search session that produced the offers on this leg;
/// it must accompany the selected OfferId when adding the segment to the basket.
/// </summary>
public sealed class ConnectingLeg
{
    public Guid SessionId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public IReadOnlyList<CabinSearchResult> Cabins { get; init; } = [];
}

// ── Slice search response types ───────────────────────────────────────────────


public sealed class SearchResponse
{
    public Guid SessionId { get; init; }
    public IReadOnlyList<FlightSearchResult> Flights { get; init; } = [];
}

public sealed class FlightSearchResult
{
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
    public string DepartureTime { get; init; } = string.Empty;
    public string ArrivalTime { get; init; } = string.Empty;
    public int ArrivalDayOffset { get; init; }
    public string AircraftType { get; init; } = string.Empty;
    public IReadOnlyList<CabinSearchResult> Cabins { get; init; } = [];
}

public sealed class CabinSearchResult
{
    public string CabinCode { get; init; } = string.Empty;
    public int AvailableSeats { get; init; }
    public decimal FromPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int? FromPoints { get; init; }
    public IReadOnlyList<FareFamilyOffer> FareFamilies { get; init; } = [];
}

/// <summary>
/// A single fare family option within a cabin, containing one offer.
/// The OfferId inside offer is passed to basket creation when the customer selects this fare.
/// </summary>
public sealed class FareFamilyOffer
{
    public string FareFamily { get; init; } = string.Empty;
    public FareOffer Offer { get; init; } = new();
}

public sealed class FareOffer
{
    public Guid OfferId { get; init; }
    public string FareBasisCode { get; init; } = string.Empty;
    public decimal BasePrice { get; init; }
    public decimal Tax { get; init; }
    public decimal TotalPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsRefundable { get; init; }
    public bool IsChangeable { get; init; }
}
