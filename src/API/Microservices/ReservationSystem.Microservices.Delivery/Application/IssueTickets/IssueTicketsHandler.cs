using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.IssueTickets;

public sealed class IssueTicketsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<IssueTicketsHandler> _logger;

    public IssueTicketsHandler(ITicketRepository ticketRepository, ILogger<IssueTicketsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<IssueTicketsResponse> HandleAsync(IssueTicketsRequest request, CancellationToken cancellationToken = default)
    {
        var ticketSummaries = new List<TicketSummary>();
        var segmentIds = request.Segments.Select(s => s.SegmentId).ToList();

        foreach (var passenger in request.Passengers)
        {
            var ticket = await IssueTicketAsync(request.BookingReference, passenger, request.Segments, cancellationToken);
            ticketSummaries.Add(DeliveryMapper.ToTicketSummary(ticket, segmentIds));
            _logger.LogInformation("Issued ticket {TicketNumber} for {PassengerId} covering {SegmentCount} segment(s)",
                ticket.TicketNumber, passenger.PassengerId, request.Segments.Count);
        }

        return new IssueTicketsResponse { Tickets = ticketSummaries };
    }

    private async Task<Ticket> IssueTicketAsync(
        string bookingReference,
        PassengerDetail passenger,
        List<SegmentDetail> segments,
        CancellationToken cancellationToken)
    {
        var ticketDataJson = BuildTicketDataJson(passenger, segments);
        // TicketNumber is assigned by the database IDENTITY on INSERT;
        // EF Core reads it back automatically via SCOPE_IDENTITY().
        var ticket = Ticket.Create(bookingReference, passenger.PassengerId, ticketDataJson);
        await _ticketRepository.CreateAsync(ticket, cancellationToken);
        return ticket;
    }

    private static string BuildTicketDataJson(PassengerDetail passenger, List<SegmentDetail> segments)
    {
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
                status = "O",
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
                fareComponent = segment.FareComponent != null
                    ? new { amount = segment.FareComponent.Amount, currency = segment.FareComponent.Currency }
                    : (object?)null
            };
        }).ToList();

        var ssrCodes = segments
            .SelectMany(s => s.SsrCodes ?? [])
            .Where(s => s.PassengerId == passenger.PassengerId)
            .Select(s => new { code = s.Code, description = s.Description, segmentRef = s.SegmentRef })
            .ToList();

        var ticketData = new
        {
            passenger = new
            {
                surname = passenger.Surname,
                givenName = passenger.GivenName,
                passengerTypeCode = passenger.PassengerTypeCode ?? "ADT",
                frequentFlyer = passenger.FrequentFlyer != null
                    ? new { carrier = passenger.FrequentFlyer.Carrier, number = passenger.FrequentFlyer.Number, tier = passenger.FrequentFlyer.Tier }
                    : (object?)null
            },
            fareConstruction = passenger.FareConstruction != null
                ? new
                {
                    pricingCurrency = passenger.FareConstruction.PricingCurrency,
                    collectingCurrency = passenger.FareConstruction.CollectingCurrency,
                    baseFare = passenger.FareConstruction.BaseFare,
                    equivalentFarePaid = passenger.FareConstruction.EquivalentFarePaid,
                    nucAmount = passenger.FareConstruction.NucAmount,
                    roeApplied = passenger.FareConstruction.RoeApplied,
                    fareCalculationLine = passenger.FareConstruction.FareCalculationLine,
                    taxes = passenger.FareConstruction.Taxes.Select(t => new { code = t.Code, amount = t.Amount, currency = t.Currency, description = t.Description }),
                    totalTaxes = passenger.FareConstruction.TotalTaxes,
                    totalAmount = passenger.FareConstruction.TotalAmount
                }
                : (object?)null,
            formOfPayment = passenger.FormOfPayment != null
                ? new
                {
                    type = passenger.FormOfPayment.Type,
                    cardType = passenger.FormOfPayment.CardType,
                    maskedPan = passenger.FormOfPayment.MaskedPan,
                    expiryMmYy = passenger.FormOfPayment.ExpiryMmYy,
                    approvalCode = passenger.FormOfPayment.ApprovalCode,
                    amount = passenger.FormOfPayment.Amount,
                    currency = passenger.FormOfPayment.Currency
                }
                : (object?)null,
            commission = passenger.Commission != null
                ? new { type = passenger.Commission.Type, rate = passenger.Commission.Rate, amount = passenger.Commission.Amount }
                : new { type = "PERCENT", rate = 0m, amount = 0m },
            endorsementsRestrictions = passenger.EndorsementsRestrictions,
            tourCode = (string?)null,
            originalIssue = new
            {
                ticketNumber = (string?)null,
                issueDate = (string?)null,
                issuingLocation = (string?)null,
                fareAmount = (decimal?)null
            },
            coupons,
            ssrCodes,
            changeHistory = new[] { new { eventType = "Issued", occurredAt = DateTime.UtcNow.ToString("o"), actor = "RetailAPI", detail = "Initial ticket issuance" } }
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
