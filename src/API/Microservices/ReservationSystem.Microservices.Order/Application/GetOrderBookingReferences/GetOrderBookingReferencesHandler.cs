using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.GetOrderBookingReferences;

/// <summary>
/// Handles the <see cref="GetOrderBookingReferencesQuery"/>.
/// Batch-resolves booking references for a list of order IDs.
/// </summary>
public sealed class GetOrderBookingReferencesHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<GetOrderBookingReferencesHandler> _logger;

    public GetOrderBookingReferencesHandler(
        IOrderRepository repository,
        ILogger<GetOrderBookingReferencesHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> HandleAsync(
        GetOrderBookingReferencesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Resolving booking references for {Count} order IDs",
            query.OrderIds.Count);

        return await _repository.GetBookingReferencesByIdsAsync(query.OrderIds, cancellationToken);
    }
}
