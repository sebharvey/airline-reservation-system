using Microsoft.Extensions.Logging;

namespace ReservationSystem.Microservices.Seat.Application.GetSeatOffer;

/// <summary>
/// Handles the <see cref="GetSeatOfferQuery"/>.
/// Retrieves a single seat offer by its identifier.
/// </summary>
public sealed class GetSeatOfferHandler
{
    private readonly ILogger<GetSeatOfferHandler> _logger;

    public GetSeatOfferHandler(ILogger<GetSeatOfferHandler> logger)
    {
        _logger = logger;
    }

    public Task<object?> HandleAsync(GetSeatOfferQuery query, CancellationToken cancellationToken = default)
    {
        // TODO: Implement seat offer validation logic in Function layer
        return Task.FromResult<object?>(null);
    }
}
