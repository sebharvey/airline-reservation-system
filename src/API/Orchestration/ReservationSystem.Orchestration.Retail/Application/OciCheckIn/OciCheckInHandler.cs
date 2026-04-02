using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.OciCheckIn;

public sealed class OciCheckInHandler
{
    private readonly DeliveryServiceClient _deliveryServiceClient;
    private readonly ILogger<OciCheckInHandler> _logger;

    public OciCheckInHandler(DeliveryServiceClient deliveryServiceClient, ILogger<OciCheckInHandler> logger)
    {
        _deliveryServiceClient = deliveryServiceClient;
        _logger = logger;
    }

    public async Task<OciCheckInResponse> HandleAsync(OciCheckInCommand command, CancellationToken cancellationToken)
    {
        var passengers = command.Passengers
            .Select(p => new BoardingCardPassengerRequest
            {
                PassengerId = p.PassengerId,
                InventoryIds = p.InventoryIds
            })
            .ToList();

        var result = await _deliveryServiceClient.CreateBoardingCardsAsync(
            command.BookingReference, passengers, cancellationToken);

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
            "OCI check-in completed for booking {BookingRef}: {Count} boarding pass(es) generated",
            command.BookingReference, boardingPasses.Count);

        return new OciCheckInResponse { BoardingPasses = boardingPasses };
    }
}
