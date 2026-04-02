using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Mappers;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.GetTicketsByBooking;

public sealed class GetTicketsByBookingHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<GetTicketsByBookingHandler> _logger;

    public GetTicketsByBookingHandler(ITicketRepository ticketRepository, ILogger<GetTicketsByBookingHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GetTicketResponse>> HandleAsync(string bookingReference, CancellationToken cancellationToken = default)
    {
        var tickets = await _ticketRepository.GetByBookingReferenceAsync(bookingReference, cancellationToken);
        return tickets.Select(DeliveryMapper.ToGetTicketResponse).ToList().AsReadOnly();
    }
}
