using Microsoft.Extensions.Logging;
using ReservationSystem.Orchestration.Operations.Application.CheckIn;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Orchestration.Operations.Application.OciPax;

public sealed record OciPaxTravelDocument(
    string Type,
    string Number,
    string IssuingCountry,
    string Nationality,
    string IssueDate,
    string ExpiryDate);

public sealed record OciPaxPassenger(string TicketNumber, OciPaxTravelDocument TravelDocument);

public sealed record OciPaxCommand(
    string BookingReference,
    string DepartureAirport,
    IReadOnlyList<OciPaxPassenger> Passengers);

public sealed record OciPaxResult(
    string BookingReference,
    bool Success,
    string? ErrorMessage = null);

public sealed class OciPaxHandler
{
    private readonly OrderServiceClient _orderServiceClient;
    private readonly ILogger<OciPaxHandler> _logger;

    public OciPaxHandler(
        OrderServiceClient orderServiceClient,
        ILogger<OciPaxHandler> logger)
    {
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    public async Task<OciPaxResult?> HandleAsync(OciPaxCommand command, CancellationToken ct)
    {
        var order = await _orderServiceClient.GetOrderAsync(command.BookingReference, ct);

        if (order is null)
        {
            _logger.LogWarning("OCI pax: order not found for {BookingReference}", command.BookingReference);
            return null;
        }

        var ticketToPaxId = CheckInHelper.ParseOrderLookups(order.OrderData).TicketToPaxId;

        var passengerUpdates = new List<PassengerDocUpdate>();

        foreach (var paxRequest in command.Passengers)
        {
            if (!ticketToPaxId.TryGetValue(paxRequest.TicketNumber, out var passengerId))
            {
                _logger.LogWarning(
                    "OCI pax: ticket {TicketNumber} not found on booking {BookingReference}",
                    paxRequest.TicketNumber, command.BookingReference);
                return new OciPaxResult(
                    command.BookingReference, false,
                    $"Ticket number '{paxRequest.TicketNumber}' was not found on booking '{command.BookingReference}'.");
            }

            passengerUpdates.Add(new PassengerDocUpdate
            {
                PassengerId = passengerId,
                Docs =
                [
                    new PassengerDoc
                    {
                        Type           = paxRequest.TravelDocument.Type,
                        Number         = paxRequest.TravelDocument.Number,
                        IssuingCountry = paxRequest.TravelDocument.IssuingCountry,
                        Nationality    = paxRequest.TravelDocument.Nationality,
                        IssueDate      = paxRequest.TravelDocument.IssueDate,
                        ExpiryDate     = paxRequest.TravelDocument.ExpiryDate
                    }
                ]
            });
        }

        if (passengerUpdates.Count > 0)
        {
            try
            {
                await _orderServiceClient.UpdateOrderPassengersAsync(command.BookingReference, passengerUpdates, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update passengers for order {BookingReference}", command.BookingReference);
                return new OciPaxResult(command.BookingReference, false);
            }
        }

        return new OciPaxResult(command.BookingReference, true);
    }
}
