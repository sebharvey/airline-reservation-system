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

        // Marks manifest entries as checked-in and generates boarding card records in the Delivery MS.
        // The boarding passes themselves are retrieved separately via POST /v1/oci/boardingpasses.
        var result = await _deliveryServiceClient.CreateBoardingCardsAsync(
            command.BookingReference, passengers, cancellationToken);

        _logger.LogInformation(
            "OCI check-in completed for booking {BookingRef}: {Count} passenger segment(s) checked in",
            command.BookingReference, result.BoardingCards.Count);

        return new OciCheckInResponse
        {
            Status = "Success",
            BookingReference = command.BookingReference,
            Message = "Check-in completed successfully"
        };
    }
}
