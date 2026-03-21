using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.GetManifest;

/// <summary>
/// Handles the <see cref="GetManifestQuery"/>.
/// Retrieves a manifest by its identifier.
/// </summary>
public sealed class GetManifestHandler
{
    private readonly IManifestRepository _repository;
    private readonly ILogger<GetManifestHandler> _logger;

    public GetManifestHandler(
        IManifestRepository repository,
        ILogger<GetManifestHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Manifest?> HandleAsync(
        GetManifestQuery query,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
