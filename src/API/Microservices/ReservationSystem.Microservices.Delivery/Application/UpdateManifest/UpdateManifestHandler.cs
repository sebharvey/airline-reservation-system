using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifest;

/// <summary>
/// Handles the <see cref="UpdateManifestCommand"/>.
/// Updates the data payload of an existing manifest.
/// </summary>
public sealed class UpdateManifestHandler
{
    private readonly IManifestRepository _repository;
    private readonly ILogger<UpdateManifestHandler> _logger;

    public UpdateManifestHandler(
        IManifestRepository repository,
        ILogger<UpdateManifestHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Manifest?> HandleAsync(
        UpdateManifestCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
