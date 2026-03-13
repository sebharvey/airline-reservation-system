using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UseCases.DeleteOffer;

public sealed class DeleteOfferHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<DeleteOfferHandler> _logger;

    public DeleteOfferHandler(
        IOfferRepository repository,
        ILogger<DeleteOfferHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <returns><c>true</c> if the offer was found and deleted; <c>false</c> if not found.</returns>
    public async Task<bool> HandleAsync(
        DeleteOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(command.Id, cancellationToken);

        if (existing is null)
        {
            _logger.LogWarning("Delete requested for unknown Offer {Id}", command.Id);
            return false;
        }

        await _repository.DeleteAsync(command.Id, cancellationToken);

        _logger.LogInformation("Deleted Offer {Id}", command.Id);

        return true;
    }
}
