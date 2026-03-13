using Microsoft.Extensions.Logging;
using Offer = global::ReservationSystem.Microservices.Offer.Domain.Entities.Offer;
using ReservationSystem.Microservices.Offer.Domain.Repositories;
using ReservationSystem.Microservices.Offer.Domain.ValueObjects;

namespace ReservationSystem.Microservices.Offer.Application.UseCases.CreateOffer;

/// <summary>
/// Handles the <see cref="CreateOfferCommand"/>.
/// Creates and persists a new <see cref="Offer"/> via the domain factory.
/// </summary>
public sealed class CreateOfferHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<CreateOfferHandler> _logger;

    public CreateOfferHandler(
        IOfferRepository repository,
        ILogger<CreateOfferHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Offer> HandleAsync(
        CreateOfferCommand command,
        CancellationToken cancellationToken = default)
    {
        var metadata = new OfferMetadata(
            command.BaggageAllowance,
            command.IsRefundable,
            command.IsChangeable,
            command.SeatsRemaining);

        var offer = Offer.Create(
            command.FlightNumber,
            command.Origin,
            command.Destination,
            command.DepartureAt,
            command.FareClass,
            command.TotalPrice,
            command.Currency,
            metadata);

        await _repository.CreateAsync(offer, cancellationToken);

        _logger.LogInformation("Created Offer {Id} for flight {FlightNumber}", offer.Id, offer.FlightNumber);

        return offer;
    }
}
