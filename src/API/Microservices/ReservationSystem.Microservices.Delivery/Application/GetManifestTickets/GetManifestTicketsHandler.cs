using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.GetManifestTickets;

/// <summary>
/// Handles the <see cref="GetManifestTicketsQuery"/>.
/// Retrieves all tickets associated with a given manifest.
/// </summary>
public sealed class GetManifestTicketsHandler
{
    private readonly ITicketRepository _repository;
    private readonly ILogger<GetManifestTicketsHandler> _logger;

    public GetManifestTicketsHandler(
        ITicketRepository repository,
        ILogger<GetManifestTicketsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Ticket>> HandleAsync(
        GetManifestTicketsQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
