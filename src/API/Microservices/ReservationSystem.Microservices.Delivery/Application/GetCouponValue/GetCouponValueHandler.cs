using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
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
    /// Returns <c>null</c> if the ticket is not found, or the coupon number is out of range.
    /// </summary>
    public async Task<GetCouponValueResponse?> HandleAsync(
        string eTicketNumber, int couponNumber, CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketRepository.GetByETicketNumberWithTaxesAsync(eTicketNumber, cancellationToken);
        if (ticket is null) return null;

        var value = ticket.GetAttributedValue(couponNumber);
        if (value is null)
        {
            _logger.LogWarning(
                "Coupon {CouponNumber} not found or fare calc unparseable on ticket {ETicketNumber}",
                couponNumber, eTicketNumber);
            return null;
        }

        return DeliveryMapper.ToCouponValueResponse(eTicketNumber, value);
    }
}
