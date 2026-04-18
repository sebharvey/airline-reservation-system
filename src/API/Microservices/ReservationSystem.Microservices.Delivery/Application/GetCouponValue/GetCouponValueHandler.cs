using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Domain.ValueObjects;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.GetCouponValue;

public sealed class GetCouponValueHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetCouponValueHandler> _logger;

    public GetCouponValueHandler(ITicketRepository ticketRepository, ILogger<GetCouponValueHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    /// <summary>
    /// Returns the attributed value for a single coupon on the given ticket.
    /// Fare share is derived from FareCalculation (typed column); tax share is summed from
    /// TicketData.fareConstruction.taxes where couponNumbers includes the requested coupon.
    /// Returns <c>null</c> if the ticket is not found, fare calculation is unparseable,
    /// or the coupon number is out of range.
    /// </summary>
    public async Task<GetCouponValueResponse?> HandleAsync(
        string eTicketNumber, int couponNumber, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByETicketNumberAsync(eTicketNumber, cancellationToken);
        if (ticket is null) return null;

        var root = JsonNode.Parse(ticket.TicketData)?.AsObject();
        var fc = root?["fareConstruction"]?.AsObject();
        if (fc is null) return null;

        var baseFare = fc["baseFare"]?.GetValue<decimal>() ?? 0m;
        var currency = fc["currency"]?.GetValue<string>() ?? string.Empty;

        if (!FareCalculation.TryParse(ticket.FareCalculation, out var fareCalc, out _) || fareCalc is null)
        {
            _logger.LogWarning("Fare calculation unparseable on ticket {ETicketNumber}", eTicketNumber);
            return null;
        }

        if (couponNumber < 1 || couponNumber > fareCalc.Components.Count)
            return null;

        var fareShare = fareCalc.GetFareShareForCoupon(couponNumber, baseFare);

        var taxShare = 0m;
        var taxesArray = fc["taxes"]?.AsArray();
        if (taxesArray is not null)
        {
            foreach (var taxNode in taxesArray)
            {
                if (taxNode is not JsonObject tax) continue;
                var couponNumbers = tax["couponNumbers"]?.AsArray();
                if (couponNumbers is null) continue;
                if (couponNumbers.Any(n => n?.GetValue<int>() == couponNumber))
                    taxShare += tax["amount"]?.GetValue<decimal>() ?? 0m;
            }
        }

        var value = new CouponValue(couponNumber, fareShare, taxShare, fareShare + taxShare, currency);
        return DeliveryMapper.ToCouponValueResponse(eTicketNumber, value);
    }
}
