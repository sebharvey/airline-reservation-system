using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.ReissueTickets;

public sealed class ReissueTicketsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<ReissueTicketsHandler> _logger;

    public ReissueTicketsHandler(ITicketRepository ticketRepository, ILogger<ReissueTicketsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<ReissueTicketsResponse> HandleAsync(ReissueTicketsRequest request, CancellationToken cancellationToken = default)
    {
        // Void old tickets.
        foreach (var eTicketNumber in request.VoidedETicketNumbers)
        {
            var oldTicket = await _ticketRepository.GetByETicketNumberAsync(eTicketNumber, cancellationToken);
            if (oldTicket is not null && !oldTicket.IsVoided)
            {
                oldTicket.Void();
                await _ticketRepository.UpdateAsync(oldTicket, cancellationToken);
                _logger.LogInformation("Voided ticket {ETicketNumber} for reissuance", eTicketNumber);
            }
        }

        // Issue replacement tickets. Fare construction is passed per passenger in the request,
        // identical to a fresh issuance. If not supplied, zero fare amounts are used so a
        // subsequent amendment can correct them.
        var ticketSummaries = new List<TicketSummary>();

        foreach (var passenger in request.Passengers)
        {
            var fc = passenger.FareConstruction;

            var ticketData = BuildReissueTicketData(passenger, request.Segments, request.Reason, request.Actor);

            var ticket = Ticket.Create(
                request.BookingReference,
                passenger.PassengerId,
                fareCalculation: fc?.FareCalculationLine ?? string.Empty,
                ticketData);

            await _ticketRepository.CreateAsync(ticket, cancellationToken);
            ticketSummaries.Add(DeliveryMapper.ToTicketSummary(ticket,
                request.Segments.Select(s => s.SegmentId).ToList()));
        }

        return new ReissueTicketsResponse
        {
            VoidedETicketNumbers = request.VoidedETicketNumbers,
            Tickets = ticketSummaries
        };
    }

    private static string BuildReissueTicketData(
        PassengerDetail passenger,
        List<SegmentDetail> segments,
        string reason,
        string actor)
    {
        var fc = passenger.FareConstruction;

        var coupons = segments.Select((segment, index) =>
        {
            var seatAssignment = segment.SeatAssignments?
                .FirstOrDefault(s => s.PassengerId == passenger.PassengerId);

            var marketingCarrier = ExtractCarrierCode(segment.FlightNumber);
            var operatingFlightNumber = segment.OperatingFlightNumber ?? segment.FlightNumber;
            var operatingCarrier = string.IsNullOrWhiteSpace(segment.OperatingFlightNumber)
                ? marketingCarrier
                : ExtractCarrierCode(segment.OperatingFlightNumber);

            return (object)new
            {
                couponNumber = index + 1,
                status = CouponStatus.Open,
                marketing = new { carrier = marketingCarrier, flightNumber = segment.FlightNumber },
                operating = new { carrier = operatingCarrier, flightNumber = operatingFlightNumber },
                origin = segment.Origin,
                destination = segment.Destination,
                departureDate = segment.DepartureDate,
                departureTime = segment.DepartureTime,
                classOfService = segment.CabinCode,
                cabin = segment.CabinName,
                fareBasisCode = segment.FareBasisCode,
                notValidBefore = segment.DepartureDate,
                notValidAfter = (string?)null,
                stopoverIndicator = segment.StopoverIndicator ?? "O",
                baggageAllowance = segment.BaggageAllowance != null
                    ? new { type = segment.BaggageAllowance.Type, quantity = segment.BaggageAllowance.Quantity, weightKg = segment.BaggageAllowance.WeightKg }
                    : (object?)null,
                seat = seatAssignment?.SeatNumber,
                attributedTaxCodes = Array.Empty<string>()
            };
        }).ToList();

        object? fareConstruction = fc is null ? null : new
        {
            baseFare = fc.BaseFare,
            currency = fc.CollectingCurrency,
            totalTaxes = fc.TotalTaxes,
            totalAmount = fc.BaseFare + fc.TotalTaxes,
            taxes = fc.Taxes.Select(t => new
            {
                code = t.Code,
                amount = t.Amount,
                currency = t.Currency.Length == 3 ? t.Currency : fc.CollectingCurrency,
                couponNumbers = Array.Empty<int>() // attribution not re-computed on reissue; use IssueTickets for new bookings
            }).ToList()
        };

        var ticketData = new
        {
            fareConstruction,
            passenger = new
            {
                surname = passenger.Surname,
                givenName = passenger.GivenName,
                passengerTypeCode = passenger.PassengerTypeCode ?? "ADT"
            },
            coupons,
            ssrCodes = Array.Empty<object>(),
            changeHistory = new[]
            {
                new
                {
                    eventType = "Reissued",
                    occurredAt = DateTime.UtcNow.ToString("o"),
                    actor,
                    detail = $"Reissued — reason: {reason}"
                }
            }
        };

        return JsonSerializer.Serialize(ticketData, SharedJsonOptions.CamelCase);
    }

    private static string ExtractCarrierCode(string flightNumber)
    {
        var i = 0;
        while (i < flightNumber.Length && char.IsLetter(flightNumber[i])) i++;
        return i > 0 ? flightNumber[0..i] : flightNumber;
    }
}
