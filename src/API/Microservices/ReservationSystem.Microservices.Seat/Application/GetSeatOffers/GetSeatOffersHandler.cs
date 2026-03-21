using Microsoft.Extensions.Logging;

namespace ReservationSystem.Microservices.Seat.Application.GetSeatOffers;

/// <summary>
/// Handles the <see cref="GetSeatOffersQuery"/>.
/// Retrieves available seat offers for a given flight.
/// </summary>
public sealed class GetSeatOffersHandler
{
    private readonly ILogger<GetSeatOffersHandler> _logger;

    public GetSeatOffersHandler(ILogger<GetSeatOffersHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(GetSeatOffersQuery query, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
