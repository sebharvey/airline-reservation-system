using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;
using ReservationSystem.Microservices.Delivery.Models.Responses;

namespace ReservationSystem.Microservices.Delivery.Application.CreateBoardingCards;

public sealed class CreateBoardingCardsHandler
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ILogger<CreateBoardingCardsHandler> _logger;

    public CreateBoardingCardsHandler(ITicketRepository ticketRepository, ILogger<CreateBoardingCardsHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    public Task<CreateBoardingCardsResponse> HandleAsync(CreateBoardingCardsRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
