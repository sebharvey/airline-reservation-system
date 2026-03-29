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
            var departureDateTime = DateTime.Parse(
                $"{f.DepartureDate}T{f.DepartureTime}:00", null,
                System.Globalization.DateTimeStyles.RoundtripKind);

            var arrivalDate = DateOnly.Parse(f.DepartureDate).AddDays(f.ArrivalDayOffset);
            var arrivalDateTime = DateTime.Parse(
                $"{arrivalDate:yyyy-MM-dd}T{f.ArrivalTime}:00", null,
                System.Globalization.DateTimeStyles.RoundtripKind);

            // Group offers by cabin, then by fare family within each cabin.
            var cabins = f.Offers
                .GroupBy(o => o.CabinCode)
                .Select(cabinGroup => new CabinSearchResult
                {
                    CabinCode      = cabinGroup.Key,
                    AvailableSeats = cabinGroup.Max(o => o.SeatsAvailable),
                    FareFamilies   = cabinGroup
                        .GroupBy(o => o.FareFamily ?? o.FareBasisCode)
                        .Select(ffGroup =>
                        {
                            // cheapest by total price; used for both the headline "from" values
                            // and as the single representative offer shown in the response.
                            var cheapest = ffGroup.MinBy(o => o.TotalAmount)!;
                            var lowestPoints = ffGroup
                                .Where(o => o.PointsPrice.HasValue)
                                .MinBy(o => o.PointsPrice)?.PointsPrice;

                            return new FareFamilyOffer
                            {
                                FareFamily      = ffGroup.Key,
                                TotalFromPrice  = cheapest.TotalAmount,
                                Currency        = cheapest.CurrencyCode,
                                TotalFromPoints = lowestPoints,
                                Offer           = new FareOffer
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
                        }).ToList()
                }).ToList();

            return new FlightSearchResult
            {
                FlightNumber  = f.FlightNumber,
                Origin        = f.Origin,
                Destination   = f.Destination,
                DepartureTime = departureDateTime,
                ArrivalTime   = arrivalDateTime,
                Cabins        = cabins
            };
        }).ToList();

        return new SearchResponse { Flights = flights };
    }
}
