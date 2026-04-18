using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Domain.Services;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;
using ReservationSystem.Shared.Common.Json;

namespace ReservationSystem.Microservices.Delivery.Application.IssueTickets;

public sealed class IssueTicketsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly TaxAttributionService _taxAttribution;
    private readonly IssueTicketsRequestValidator _validator;
    private readonly ILogger<IssueTicketsHandler> _logger;

    public IssueTicketsHandler(
        ITicketRepository ticketRepository,
        TaxAttributionService taxAttribution,
        ILogger<IssueTicketsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _taxAttribution = taxAttribution;
        _validator = new IssueTicketsRequestValidator();
        _logger = logger;
    }

    public async Task<IssueTicketsResponse> HandleAsync(IssueTicketsRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = _validator.Validate(request);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var segmentIds = request.Segments.Select(s => s.SegmentId).ToList();
        var ticketSummaries = new List<TicketSummary>();

        // Build coupon itinerary once — shared across all passengers (same flight segments).
        var itinerary = request.Segments.Select((seg, idx) => new CouponItinerary(
            CouponNumber: idx + 1,
            Origin: seg.Origin,
            Destination: seg.Destination
        )).ToList();

        foreach (var passenger in request.Passengers)
        {
            var ticket = await IssueTicketForPassengerAsync(
                request.BookingReference, passenger, request.Segments, itinerary, cancellationToken);

            ticketSummaries.Add(DeliveryMapper.ToTicketSummary(ticket, segmentIds));
            _logger.LogInformation(
                "Issued ticket {TicketNumber} for {PassengerId} covering {SegmentCount} segment(s)",
                ticket.TicketNumber, passenger.PassengerId, request.Segments.Count);
        }

        return new IssueTicketsResponse { Tickets = ticketSummaries };
    }

    private async Task<Ticket> IssueTicketForPassengerAsync(
        string bookingReference,
        PassengerDetail passenger,
        List<SegmentDetail> segments,
        List<CouponItinerary> itinerary,
        CancellationToken cancellationToken)
    {
        var fc = passenger.FareConstruction!; // validator guarantees non-null

        // Build TicketData JSON (operational data only — no fare amounts).
        string ticketData = BuildTicketData(passenger, segments, fc);

        var ticket = Ticket.Create(
            bookingReference,
            passenger.PassengerId,
            totalFareAmount: fc.BaseFare,
            currency: fc.CollectingCurrency,
            totalTaxAmount: fc.TotalTaxes,
            fareCalculation: fc.FareCalculationLine,
            ticketData);

        // Derive coupon attribution for each tax and attach to the ticket aggregate.
        // For split taxes (e.g. YQ), AttributeTax returns one group per coupon with
        // the per-coupon amount so GetAttributedValue can sum without double-counting.
        string taxCurrency = fc.CollectingCurrency;
        foreach (var tax in fc.Taxes)
        {
            string effectiveCurrency = tax.Currency.Length == 3 ? tax.Currency : taxCurrency;
            var groups = _taxAttribution.AttributeTax(tax.Code, tax.Amount, itinerary);
            foreach (var (amount, couponNumbers) in groups)
            {
                if (couponNumbers.Count == 0) continue;
                var ticketTax = TicketTax.Create(ticket.TicketId, tax.Code, amount, effectiveCurrency, couponNumbers);
                ticket.AddTax(ticketTax);
            }
        }

        await _ticketRepository.CreateAsync(ticket, cancellationToken);
        return ticket;
    }

    private string BuildTicketData(PassengerDetail passenger, List<SegmentDetail> segments, FareConstructionDetail fc)
    {
        // Derive attributed tax codes per coupon so we can embed them in each coupon object.
        var itinerary = segments.Select((seg, idx) => new CouponItinerary(
            CouponNumber: idx + 1,
            Origin: seg.Origin,
            Destination: seg.Destination
        )).ToList();

        var taxCodesByCoupon = new Dictionary<int, List<string>>();
        foreach (var tax in fc.Taxes)
        {
            var groups = _taxAttribution.AttributeTax(tax.Code, tax.Amount, itinerary);
            foreach (var (_, couponNumbers) in groups)
            {
                foreach (var n in couponNumbers)
                {
                    if (!taxCodesByCoupon.TryGetValue(n, out var list))
                        taxCodesByCoupon[n] = list = [];
                    if (!list.Contains(tax.Code))
                        list.Add(tax.Code);
                }
            }
        }

        var coupons = segments.Select((segment, index) =>
        {
            var couponNumber = index + 1;
            var seatAssignment = segment.SeatAssignments?
                .FirstOrDefault(s => s.PassengerId == passenger.PassengerId);

            var marketingCarrier = ExtractCarrierCode(segment.FlightNumber);
            var operatingFlightNumber = segment.OperatingFlightNumber ?? segment.FlightNumber;
            var operatingCarrier = string.IsNullOrWhiteSpace(segment.OperatingFlightNumber)
                ? marketingCarrier
                : ExtractCarrierCode(segment.OperatingFlightNumber);

            var attributedTaxCodes = taxCodesByCoupon.TryGetValue(couponNumber, out var codes)
                ? (object)codes
                : Array.Empty<string>();

            return (object)new
            {
                couponNumber,
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
                    ? new
                    {
                        type = segment.BaggageAllowance.Type,
                        quantity = segment.BaggageAllowance.Quantity,
                        weightKg = segment.BaggageAllowance.WeightKg
                    }
                    : (object?)null,
                seat = seatAssignment?.SeatNumber,
                // attributedTaxCodes indicates which taxes from the ticket-level breakdown
                // apply to this coupon. Value is derived; do not treat as authoritative amounts.
                attributedTaxCodes
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
                    ? new
                    {
                        carrier = passenger.FrequentFlyer.Carrier,
                        number = passenger.FrequentFlyer.Number,
                        tier = passenger.FrequentFlyer.Tier
                    }
                    : (object?)null
            },
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
            changeHistory = new[]
            {
                new
                {
                    eventType = "Issued",
                    occurredAt = DateTime.UtcNow.ToString("o"),
                    actor = "RetailAPI",
                    detail = "Initial ticket issuance"
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
