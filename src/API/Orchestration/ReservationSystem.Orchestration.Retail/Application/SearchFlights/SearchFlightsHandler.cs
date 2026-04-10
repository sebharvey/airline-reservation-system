using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.SearchFlights;

/// <summary>
/// Handles POST /v1/search/slice.
///
/// Returns a unified <see cref="SliceSearchResponse"/> regardless of whether the
/// route is served by a direct flight or requires a connection via LHR.
///
/// Strategy:
/// 1. Search the Offer MS for a direct flight on the requested origin → destination.
/// 2. If direct flights are found, wrap each as a single-leg <see cref="SliceItinerary"/>.
/// 3. If no direct flights exist and neither endpoint is LHR, automatically fall back to
///    a connecting search via LHR: search origin → LHR (leg 1) and LHR → destination
///    (leg 2), pair legs that satisfy the 60-minute MCT, and wrap each valid pair as a
///    two-leg <see cref="SliceItinerary"/>.
/// 4. Return an empty itinerary list if neither strategy produces results.
///
/// The Offer MS has no concept of multi-leg itineraries; all connecting assembly
/// is done here in the Retail orchestration layer.
/// </summary>
public sealed class SearchFlightsHandler
{
    private const int MinimumConnectionTimeMinutes = 60;
    private const string Hub = "LHR";

    private readonly OfferServiceClient _offerServiceClient;

    public SearchFlightsHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<SliceSearchResponse> HandleAsync(
        SearchFlightsCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: try a direct search ───────────────────────────────────────
        var directResult = await _offerServiceClient.SearchAsync(
            command.Origin,
            command.Destination,
            command.DepartureDate,
            command.PaxCount,
            command.BookingType,
            cancellationToken);

        if (directResult.Flights.Count > 0)
            return BuildDirectResponse(directResult);

        // ── Step 2: connecting fallback (only when neither endpoint is LHR) ───
        var isOriginHub      = command.Origin.Equals(Hub, StringComparison.OrdinalIgnoreCase);
        var isDestinationHub = command.Destination.Equals(Hub, StringComparison.OrdinalIgnoreCase);

        if (isOriginHub || isDestinationHub)
            return new SliceSearchResponse { Itineraries = [] };

        if (!DateOnly.TryParse(command.DepartureDate, out var depDate))
            return new SliceSearchResponse { Itineraries = [] };

        return await BuildConnectingResponseAsync(command, depDate, cancellationToken);
    }

    // ── Direct response builder ───────────────────────────────────────────────

    private static SliceSearchResponse BuildDirectResponse(
        OfferSearchResultDto result)
    {
        var itineraries = result.Flights.Select(f =>
        {
            var cabins = BuildCabins(f.Offers);
            var cheapestPrice = cabins.Count > 0 ? cabins.Min(c => c.FromPrice) : 0m;
            var currency      = cabins.Count > 0 ? cabins[0].Currency : "GBP";

            return new SliceItinerary
            {
                Legs = [new SliceLeg
                {
                    SessionId        = result.SessionId,
                    FlightNumber     = f.FlightNumber,
                    Origin           = f.Origin,
                    Destination      = f.Destination,
                    DepartureDate    = f.DepartureDate,
                    DepartureTime    = f.DepartureTime,
                    ArrivalTime      = f.ArrivalTime,
                    ArrivalDayOffset = f.ArrivalDayOffset,
                    AircraftType     = f.AircraftType,
                    Cabins           = cabins
                }],
                ConnectionDurationMinutes = null,
                CombinedFromPrice = cheapestPrice,
                Currency          = currency
            };
        }).ToList();

        return new SliceSearchResponse { Itineraries = itineraries };
    }

    // ── Connecting response builder ───────────────────────────────────────────

    private async Task<SliceSearchResponse> BuildConnectingResponseAsync(
        SearchFlightsCommand command,
        DateOnly depDate,
        CancellationToken cancellationToken)
    {
        // Leg 1: origin → LHR on the requested departure date.
        var leg1Result = await _offerServiceClient.SearchAsync(
            command.Origin, Hub, command.DepartureDate,
            command.PaxCount, command.BookingType, cancellationToken);

        if (leg1Result.Flights.Count == 0)
            return new SliceSearchResponse { Itineraries = [] };

        // Derive the unique LHR arrival dates across all leg-1 flights, then search
        // each in parallel for the leg-2 segment (LHR → destination).
        var uniqueArrivalDates = leg1Result.Flights
            .Select(f => depDate.AddDays(f.ArrivalDayOffset))
            .Distinct()
            .ToList();

        var leg2Tasks = uniqueArrivalDates.Select(d =>
            _offerServiceClient.SearchAsync(
                Hub, command.Destination, d.ToString("yyyy-MM-dd"),
                command.PaxCount, command.BookingType, cancellationToken));

        var leg2ResultsArray = await Task.WhenAll(leg2Tasks);

        var leg2ByArrivalDate = uniqueArrivalDates
            .Zip(leg2ResultsArray)
            .ToDictionary(x => x.First, x => x.Second);

        // Pair legs, applying MCT filter.
        var itineraries = new List<SliceItinerary>();

        foreach (var leg1Flight in leg1Result.Flights)
        {
            if (!TimeOnly.TryParse(leg1Flight.ArrivalTime, out var leg1ArrivalTime)) continue;

            var leg1ArrivalDate = depDate.AddDays(leg1Flight.ArrivalDayOffset);
            var leg1ArrivalDt   = leg1ArrivalDate.ToDateTime(leg1ArrivalTime);

            if (!leg2ByArrivalDate.TryGetValue(leg1ArrivalDate, out var leg2Search)) continue;
            if (leg2Search.Flights.Count == 0) continue;

            var leg1Cabins = BuildCabins(leg1Flight.Offers);

            foreach (var leg2Flight in leg2Search.Flights)
            {
                if (!TimeOnly.TryParse(leg2Flight.DepartureTime, out var leg2DepTime)) continue;

                var leg2DepDt       = leg1ArrivalDate.ToDateTime(leg2DepTime);
                var connectionMins  = (int)(leg2DepDt - leg1ArrivalDt).TotalMinutes;
                if (connectionMins < MinimumConnectionTimeMinutes) continue;

                var leg2Cabins        = BuildCabins(leg2Flight.Offers);
                var leg1CheapestPrice = leg1Cabins.Count > 0 ? leg1Cabins.Min(c => c.FromPrice) : 0m;
                var leg2CheapestPrice = leg2Cabins.Count > 0 ? leg2Cabins.Min(c => c.FromPrice) : 0m;
                var currency          = leg1Cabins.Count > 0 ? leg1Cabins[0].Currency : "GBP";

                var leg2DepartureDate = !string.IsNullOrWhiteSpace(leg2Flight.DepartureDate)
                    ? leg2Flight.DepartureDate
                    : leg1ArrivalDate.ToString("yyyy-MM-dd");

                itineraries.Add(new SliceItinerary
                {
                    Legs =
                    [
                        new SliceLeg
                        {
                            SessionId        = leg1Result.SessionId,
                            FlightNumber     = leg1Flight.FlightNumber,
                            Origin           = leg1Flight.Origin,
                            Destination      = leg1Flight.Destination,
                            DepartureDate    = !string.IsNullOrWhiteSpace(leg1Flight.DepartureDate)
                                                   ? leg1Flight.DepartureDate
                                                   : command.DepartureDate,
                            DepartureTime    = leg1Flight.DepartureTime,
                            ArrivalTime      = leg1Flight.ArrivalTime,
                            ArrivalDayOffset = leg1Flight.ArrivalDayOffset,
                            AircraftType     = leg1Flight.AircraftType,
                            Cabins           = leg1Cabins
                        },
                        new SliceLeg
                        {
                            SessionId        = leg2Search.SessionId,
                            FlightNumber     = leg2Flight.FlightNumber,
                            Origin           = leg2Flight.Origin,
                            Destination      = leg2Flight.Destination,
                            DepartureDate    = leg2DepartureDate,
                            DepartureTime    = leg2Flight.DepartureTime,
                            ArrivalTime      = leg2Flight.ArrivalTime,
                            ArrivalDayOffset = leg2Flight.ArrivalDayOffset,
                            AircraftType     = leg2Flight.AircraftType,
                            Cabins           = leg2Cabins
                        }
                    ],
                    ConnectionDurationMinutes = connectionMins,
                    CombinedFromPrice         = leg1CheapestPrice + leg2CheapestPrice,
                    Currency                  = currency
                });
            }
        }

        itineraries.Sort((a, b) =>
            (a.ConnectionDurationMinutes ?? 0).CompareTo(b.ConnectionDurationMinutes ?? 0));

        return new SliceSearchResponse { Itineraries = itineraries };
    }

    // ── Shared cabin builder (same logic as SearchConnectingFlightsHandler) ───

    private static IReadOnlyList<CabinSearchResult> BuildCabins(IReadOnlyList<OfferItemDto> offers)
    {
        return offers
            .GroupBy(o => o.CabinCode)
            .Select(cabinGroup =>
            {
                var cheapest     = cabinGroup.MinBy(o => o.TotalAmount)!;
                var lowestPoints = cabinGroup.Where(o => o.PointsPrice.HasValue).MinBy(o => o.PointsPrice)?.PointsPrice;

                var fareFamilies = cabinGroup
                    .GroupBy(o => o.FareFamily ?? o.FareBasisCode)
                    .Select(ffGroup =>
                    {
                        var ff = ffGroup.MinBy(o => o.TotalAmount)!;
                        return new FareFamilyOffer
                        {
                            FareFamily = ffGroup.Key,
                            Offer      = new FareOffer
                            {
                                OfferId       = ff.OfferId,
                                FareBasisCode = ff.FareBasisCode,
                                BasePrice     = ff.BaseFareAmount,
                                Tax           = ff.TaxAmount,
                                TotalPrice    = ff.TotalAmount,
                                Currency      = ff.CurrencyCode,
                                IsRefundable  = ff.IsRefundable,
                                IsChangeable  = ff.IsChangeable
                            }
                        };
                    }).ToList();

                return new CabinSearchResult
                {
                    CabinCode      = cabinGroup.Key,
                    AvailableSeats = cabinGroup.Max(o => o.SeatsAvailable),
                    FromPrice      = cheapest.TotalAmount,
                    Currency       = cheapest.CurrencyCode,
                    FromPoints     = lowestPoints,
                    FareFamilies   = fareFamilies
                };
            }).ToList();
    }
}
