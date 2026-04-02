using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.OciBoardingPasses;

public sealed class OciBoardingPassesHandler
{
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<OciBoardingPassesHandler> _logger;

    public OciBoardingPassesHandler(DeliveryServiceClient deliveryServiceClient, ILogger<OciBoardingPassesHandler> logger)
    {
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    public async Task<OciBoardingPassesResponse> HandleAsync(OciBoardingPassesQuery query, CancellationToken cancellationToken)
    {
        var result = await _deliveryServiceClient.GetBoardingCardsByBookingAsync(
            query.BookingReference, cancellationToken);

        var boardingPasses = result.BoardingCards
            .Select(bc => new OciBoardingPass
            {
                BookingReference = bc.BookingReference,
                PassengerId = bc.PassengerId,
                GivenName = bc.GivenName,
                Surname = bc.Surname,
                FlightNumber = bc.FlightNumber,
                Origin = bc.Origin,
                Destination = bc.Destination,
                DepartureDateTime = bc.DepartureDateTime,
                SeatNumber = bc.SeatNumber,
                CabinCode = bc.CabinCode,
                ETicketNumber = bc.ETicketNumber,
                SequenceNumber = bc.SequenceNumber,
                BcbpBarcode = bc.BcbpString,
                Gate = bc.Gate,
                BoardingTime = bc.BoardingTime
            })
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} boarding pass(es) for booking {BookingRef}",
            boardingPasses.Count, query.BookingReference);

        return new OciBoardingPassesResponse { BoardingPasses = boardingPasses };
    }
}
