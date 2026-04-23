using System.Text.Json;
using System.Text.Json.Nodes;
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
        // Void old tickets. Collect them keyed by PassengerId so the retained coupons and
        // originalIssue data can be merged into the replacement ticket for each passenger.
        var oldTicketsByPassenger = new Dictionary<string, (string ETicketNumber, Ticket Ticket)?>(StringComparer.OrdinalIgnoreCase);

        foreach (var eTicketNumber in request.VoidedETicketNumbers)
        {
            var oldTicket = await _ticketRepository.GetByETicketNumberAsync(eTicketNumber, cancellationToken);
            if (oldTicket is null || oldTicket.IsVoided) continue;

            // Mark the coupon(s) for the routes being replaced as EXCHANGED before voiding,
            // so the status is correct on the voided ticket record and retained coupons can
            // be identified by exclusion.
            foreach (var seg in request.Segments)
                oldTicket.UpdateCouponStatus(string.Empty, seg.Origin, seg.Destination, CouponStatus.Exchanged, request.Actor);

            oldTicket.Void();
            await _ticketRepository.UpdateAsync(oldTicket, cancellationToken);
            _logger.LogInformation("Voided ticket {ETicketNumber} for reissuance", eTicketNumber);

            oldTicketsByPassenger[oldTicket.PassengerId] = (eTicketNumber, oldTicket);
        }

        // Issue replacement tickets. Fare construction is passed per passenger in the request,
        // identical to a fresh issuance. If not supplied, zero fare amounts are used so a
        // subsequent amendment can correct them.
        var ticketSummaries = new List<TicketSummary>();

        foreach (var passenger in request.Passengers)
        {
            oldTicketsByPassenger.TryGetValue(passenger.PassengerId, out var oldEntry);

            var ticketData = BuildReissueTicketData(
                passenger,
                request.Segments,
                request.Reason,
                request.Actor,
                oldEntry?.ETicketNumber,
                oldEntry?.Ticket);

            var ticket = Ticket.Create(
                request.BookingReference,
                passenger.PassengerId,
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
        string actor,
        string? oldETicketNumber,
        Ticket? oldTicket)
    {
        var fc = passenger.FareConstruction;

        // Build the replacement coupon nodes for the new segment(s).
        var replacementCoupons = BuildReplacementCouponNodes(passenger, segments);

        // Extract retained coupons (non-replaced) and originalIssue data from the old ticket.
        var retainedCoupons = new List<JsonNode>();
        object? originalIssue = null;

        if (oldTicket is not null && oldETicketNumber is not null)
        {
            var oldRoot = JsonNode.Parse(oldTicket.TicketData)?.AsObject();
            if (oldRoot is not null)
            {
                // Coupons that are not EXCHANGED were not part of the rebooking — carry them forward.
                if (oldRoot["coupons"] is JsonArray oldCoupons)
                {
                    foreach (var couponNode in oldCoupons)
                    {
                        if (couponNode is null) continue;
                        var status = couponNode["status"]?.GetValue<string>() ?? string.Empty;
                        if (!string.Equals(status, CouponStatus.Exchanged, StringComparison.OrdinalIgnoreCase))
                            retainedCoupons.Add(JsonNode.Parse(couponNode.ToJsonString())!);
                    }
                }

                // Populate originalIssue from the voided ticket.
                var oldFc = oldRoot["fareConstruction"]?.AsObject();
                var firstCoupon = oldRoot["coupons"]?[0]?.AsObject();
                decimal? fareAmount = null;
                var fareNode = oldFc?["baseFare"];
                if (fareNode is not null) fareAmount = fareNode.GetValue<decimal>();

                originalIssue = new
                {
                    ticketNumber = oldETicketNumber,
                    issueDate = oldTicket.CreatedAt.ToString("yyyy-MM-dd"),
                    issuingLocation = firstCoupon?["origin"]?.GetValue<string>(),
                    fareAmount
                };
            }
        }

        // Merge replacement and retained coupons, then sort chronologically and renumber.
        var allCoupons = new List<JsonNode>(replacementCoupons);
        allCoupons.AddRange(retainedCoupons);
        SortAndRenumberCoupons(allCoupons);

        object? fareConstruction = fc is null ? null : new
        {
            fareCalculationLine = fc.FareCalculationLine,
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

        var detail = oldETicketNumber is not null
            ? $"Reissued — reason: {reason}; prior ticket {oldETicketNumber} voided"
            : $"Reissued — reason: {reason}";

        var ticketData = new
        {
            originalIssue,
            fareConstruction,
            passenger = new
            {
                surname = passenger.Surname,
                givenName = passenger.GivenName,
                passengerTypeCode = passenger.PassengerTypeCode ?? "ADT"
            },
            coupons = allCoupons,
            ssrCodes = Array.Empty<object>(),
            changeHistory = new[]
            {
                new
                {
                    eventType = "IROPSReissued",
                    occurredAt = DateTime.UtcNow.ToString("o"),
                    actor,
                    detail
                }
            }
        };

        return JsonSerializer.Serialize(ticketData, SharedJsonOptions.CamelCase);
    }

    private static List<JsonNode> BuildReplacementCouponNodes(PassengerDetail passenger, List<SegmentDetail> segments)
    {
        return segments.Select((segment, index) =>
        {
            var seatAssignment = segment.SeatAssignments?
                .FirstOrDefault(s => s.PassengerId == passenger.PassengerId);

            var marketingCarrier = ExtractCarrierCode(segment.FlightNumber);
            var operatingFlightNumber = segment.OperatingFlightNumber ?? segment.FlightNumber;
            var operatingCarrier = string.IsNullOrWhiteSpace(segment.OperatingFlightNumber)
                ? marketingCarrier
                : ExtractCarrierCode(segment.OperatingFlightNumber);

            var coupon = new
            {
                couponNumber = index + 1, // renumbered after merge
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

            return JsonNode.Parse(JsonSerializer.Serialize(coupon, SharedJsonOptions.CamelCase))!;
        }).ToList();
    }

    /// <summary>
    /// Sorts coupons chronologically by departure date then time, and assigns sequential coupon numbers.
    /// </summary>
    private static void SortAndRenumberCoupons(List<JsonNode> coupons)
    {
        coupons.Sort((a, b) =>
        {
            var dateA = (a as JsonObject)?["departureDate"]?.GetValue<string>() ?? string.Empty;
            var dateB = (b as JsonObject)?["departureDate"]?.GetValue<string>() ?? string.Empty;
            var cmp = string.Compare(dateA, dateB, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            var timeA = (a as JsonObject)?["departureTime"]?.GetValue<string>() ?? string.Empty;
            var timeB = (b as JsonObject)?["departureTime"]?.GetValue<string>() ?? string.Empty;
            return string.Compare(timeA, timeB, StringComparison.Ordinal);
        });

        for (var i = 0; i < coupons.Count; i++)
        {
            if (coupons[i] is JsonObject coupon)
                coupon["couponNumber"] = i + 1;
        }
    }

    private static string ExtractCarrierCode(string flightNumber)
    {
        var i = 0;
        while (i < flightNumber.Length && char.IsLetter(flightNumber[i])) i++;
        return i > 0 ? flightNumber[0..i] : flightNumber;
    }
}
