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

            var fareFamilies = f.Offers.Select(o => new FareFamilyOffer
            {
                FareFamily     = o.FareFamily ?? o.CabinCode,
                CabinCode      = o.CabinCode,
                OfferId        = o.OfferId,
                FareBasisCode  = o.FareBasisCode,
                Price          = o.TotalAmount,
                Currency       = o.CurrencyCode,
                AvailableSeats = o.SeatsAvailable,
                IsRefundable   = o.IsRefundable,
                IsChangeable   = o.IsChangeable
            }).ToList();

            return new FlightSearchResult
            {
                FlightNumber  = f.FlightNumber,
                Origin        = f.Origin,
                Destination   = f.Destination,
                DepartureTime = departureDateTime,
                ArrivalTime   = arrivalDateTime,
                FareFamilies  = fareFamilies
            };
        }).ToList();

        return new SearchResponse { Flights = flights };
    }
}
