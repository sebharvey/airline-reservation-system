using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.SearchFlights;

/// <summary>
/// Searches for connecting itinerary options via the LHR hub.
/// Calls the Offer MS for each leg independently, then pairs results that satisfy
/// the 60-minute minimum connection time (MCT) at LHR.
/// </summary>
public sealed class SearchConnectingFlightsHandler
{
    private const int MinimumConnectionTimeMinutes = 60;
    private const string Hub = "LHR";

    private readonly OfferServiceClient _offerServiceClient;

    public SearchConnectingFlightsHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<ConnectingSearchResponse> HandleAsync(
        SearchConnectingFlightsCommand command,
        CancellationToken cancellationToken)
    {
        // Only hub-and-spoke routes are supported; caller must not specify LHR as either endpoint.
        if (command.Origin.Equals(Hub, StringComparison.OrdinalIgnoreCase) ||
            command.Destination.Equals(Hub, StringComparison.OrdinalIgnoreCase))
        {
            return new ConnectingSearchResponse { Itineraries = [] };
        }

        if (!DateOnly.TryParse(command.DepartureDate, out var depDate))
            return new ConnectingSearchResponse { Itineraries = [] };

        // Step 1 — search first leg: origin → LHR.
        var leg1Result = await _offerServiceClient.SearchAsync(
            command.Origin, Hub, command.DepartureDate,
            command.PaxCount, command.BookingType, cancellationToken);

        if (leg1Result.Flights.Count == 0)
            return new ConnectingSearchResponse { Itineraries = [] };

        // Step 2 — determine unique LHR arrival dates from all leg1 results and search
        // leg2 for each in parallel.  Most routes produce a single arrival date but the
        // handler handles the general case (e.g. if overnight leg1 flights exist).
        var uniqueArrivalDates = leg1Result.Flights
            .Select(f => depDate.AddDays(f.ArrivalDayOffset))
            .Distinct()
            .ToList();

        var leg2Tasks = uniqueArrivalDates.Select(d =>
            _offerServiceClient.SearchAsync(
                Hub, command.Destination, d.ToString("yyyy-MM-dd"),
                command.PaxCount, command.BookingType, cancellationToken));

        var leg2ResultsArray = await Task.WhenAll(leg2Tasks);

        // Map: arrival date → Offer MS search result (contains flights + session id).
        var leg2ByArrivalDate = uniqueArrivalDates
            .Zip(leg2ResultsArray)
            .ToDictionary(x => x.First, x => x.Second);

        // Step 3 — pair legs and apply MCT filter.
        var itineraries = new List<ConnectingItinerary>();

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

                // leg2 departs from LHR on the same calendar day as leg1 arrives.
                var leg2DepDt = leg1ArrivalDate.ToDateTime(leg2DepTime);
                var connectionMinutes = (int)(leg2DepDt - leg1ArrivalDt).TotalMinutes;

                if (connectionMinutes < MinimumConnectionTimeMinutes) continue;

                var leg2Cabins = BuildCabins(leg2Flight.Offers);

                var leg1CheapestPrice = leg1Cabins.Count > 0 ? leg1Cabins.Min(c => c.FromPrice) : 0m;
                var leg2CheapestPrice = leg2Cabins.Count > 0 ? leg2Cabins.Min(c => c.FromPrice) : 0m;
                var currency = leg1Cabins.Count > 0 ? leg1Cabins[0].Currency : "GBP";

                // Leg2 departure date — prefer the value returned by the Offer MS; fall back to
                // the arrival date we searched against.
                var leg2DepartureDate = !string.IsNullOrWhiteSpace(leg2Flight.DepartureDate)
                    ? leg2Flight.DepartureDate
                    : leg1ArrivalDate.ToString("yyyy-MM-dd");

                itineraries.Add(new ConnectingItinerary
                {
                    Leg1 = new ConnectingLeg
                    {
                        SessionId     = leg1Result.SessionId,
                        FlightNumber  = leg1Flight.FlightNumber,
                        Origin        = leg1Flight.Origin,
                        Destination   = leg1Flight.Destination,
                        DepartureDate = !string.IsNullOrWhiteSpace(leg1Flight.DepartureDate)
                                            ? leg1Flight.DepartureDate
                                            : command.DepartureDate,
                        DepartureTime    = leg1Flight.DepartureTime,
                        ArrivalTime      = leg1Flight.ArrivalTime,
                        ArrivalDayOffset = leg1Flight.ArrivalDayOffset,
                        AircraftType     = leg1Flight.AircraftType,
                        Cabins           = leg1Cabins
                    },
                    Leg2 = new ConnectingLeg
                    {
                        SessionId     = leg2Search.SessionId,
                        FlightNumber  = leg2Flight.FlightNumber,
                        Origin        = leg2Flight.Origin,
                        Destination   = leg2Flight.Destination,
                        DepartureDate    = leg2DepartureDate,
                        DepartureTime    = leg2Flight.DepartureTime,
                        ArrivalTime      = leg2Flight.ArrivalTime,
                        ArrivalDayOffset = leg2Flight.ArrivalDayOffset,
                        AircraftType     = leg2Flight.AircraftType,
                        Cabins           = leg2Cabins
                    },
                    ConnectionDurationMinutes = connectionMinutes,
                    CombinedFromPrice = leg1CheapestPrice + leg2CheapestPrice,
                    Currency          = currency
                });
            }
        }

        // Sort by connection duration so the most efficient itineraries appear first.
        itineraries.Sort((a, b) => a.ConnectionDurationMinutes.CompareTo(b.ConnectionDurationMinutes));

        return new ConnectingSearchResponse { Itineraries = itineraries };
    }

    private static IReadOnlyList<CabinSearchResult> BuildCabins(IReadOnlyList<OfferItemDto> offers)
    {
        return offers
            .GroupBy(o => o.CabinCode)
            .Select(cabinGroup =>
            {
                var cheapest        = cabinGroup.MinBy(o => o.TotalAmount)!;
                var lowestPoints    = cabinGroup.Where(o => o.PointsPrice.HasValue).MinBy(o => o.PointsPrice)?.PointsPrice;

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
