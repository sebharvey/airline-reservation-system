using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.SearchFlights;

public sealed class SearchFlightsHandler
{
    private readonly OfferServiceClient _offerServiceClient;

    public SearchFlightsHandler(OfferServiceClient offerServiceClient)
    {
        _offerServiceClient = offerServiceClient;
    }

    public async Task<SearchResponse> HandleAsync(SearchFlightsCommand command, CancellationToken cancellationToken)
    {
        var result = await _offerServiceClient.SearchAsync(
            command.Origin,
            command.Destination,
            command.DepartureDate,
            command.PaxCount,
            command.BookingType,
            cancellationToken);

        var flights = result.Flights.Select(f =>
        {
            // Group offers by cabin, then by fare family within each cabin.
            var cabins = f.Offers
                .GroupBy(o => o.CabinCode)
                .Select(cabinGroup =>
                {
                    var cabinCheapest = cabinGroup.MinBy(o => o.TotalAmount)!;
                    var cabinLowestPoints = cabinGroup
                        .Where(o => o.PointsPrice.HasValue)
                        .MinBy(o => o.PointsPrice)?.PointsPrice;

                    var fareFamilies = cabinGroup
                        .GroupBy(o => o.FareFamily ?? o.FareBasisCode)
                        .Select(ffGroup =>
                        {
                            // cheapest within the fare family — used as the single representative offer.
                            var cheapest = ffGroup.MinBy(o => o.TotalAmount)!;

                            return new FareFamilyOffer
                            {
                                FareFamily = ffGroup.Key,
                                Offer      = new FareOffer
                                {
                                    OfferId       = cheapest.OfferId,
                                    FareBasisCode = cheapest.FareBasisCode,
                                    BasePrice     = cheapest.BaseFareAmount,
                                    Tax           = cheapest.TaxAmount,
                                    TotalPrice    = cheapest.TotalAmount,
                                    Currency      = cheapest.CurrencyCode,
                                    IsRefundable  = cheapest.IsRefundable,
                                    IsChangeable  = cheapest.IsChangeable
                                }
                            };
                        }).ToList();

                    return new CabinSearchResult
                    {
                        CabinCode      = cabinGroup.Key,
                        AvailableSeats = cabinGroup.Max(o => o.SeatsAvailable),
                        FromPrice      = cabinCheapest.TotalAmount,
                        Currency       = cabinCheapest.CurrencyCode,
                        FromPoints     = cabinLowestPoints,
                        FareFamilies   = fareFamilies
                    };
                }).ToList();

            return new FlightSearchResult
            {
                FlightNumber    = f.FlightNumber,
                Origin          = f.Origin,
                Destination     = f.Destination,
                DepartureDate   = f.DepartureDate,
                DepartureTime   = f.DepartureTime,
                ArrivalTime     = f.ArrivalTime,
                ArrivalDayOffset = f.ArrivalDayOffset,
                AircraftType    = f.AircraftType,
                Cabins          = cabins
            };
        }).ToList();

        return new SearchResponse { SessionId = result.SessionId, Flights = flights };
    }
}
