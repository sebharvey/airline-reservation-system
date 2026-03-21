using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Microservices.Delivery.Application.CreateManifest;

/// <summary>
/// Handles the <see cref="CreateManifestCommand"/>.
/// Creates and persists a new <see cref="Manifest"/>.
/// </summary>
public sealed class CreateManifestHandler
{
    private readonly IManifestRepository _repository;
    private readonly ILogger<CreateManifestHandler> _logger;

    public CreateManifestHandler(
        IManifestRepository repository,
        ILogger<CreateManifestHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Manifest> HandleAsync(
        CreateManifestCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
