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
            command.CabinCode,
            command.PaxCount,
            command.BookingType,
            cancellationToken);

        var offers = result.Offers.Select(o =>
        {
            var departureDateTime = DateTime.Parse($"{o.DepartureDate}T{o.DepartureTime}:00", null, System.Globalization.DateTimeStyles.RoundtripKind);
            var arrivalDate = DateOnly.Parse(o.DepartureDate).AddDays(o.ArrivalDayOffset);
            var arrivalDateTime = DateTime.Parse($"{arrivalDate:yyyy-MM-dd}T{o.ArrivalTime}:00", null, System.Globalization.DateTimeStyles.RoundtripKind);

            return new FlightOffer
            {
                OfferId = o.OfferId,
                FlightNumber = o.FlightNumber,
                Origin = o.Origin,
                Destination = o.Destination,
                DepartureTime = departureDateTime,
                ArrivalTime = arrivalDateTime,
                CabinClass = o.CabinCode,
                Price = o.TotalAmount,
                Currency = o.CurrencyCode,
                AvailableSeats = o.SeatsAvailable
            };
        }).ToList();

        return new SearchResponse { Offers = offers };
    }
}
