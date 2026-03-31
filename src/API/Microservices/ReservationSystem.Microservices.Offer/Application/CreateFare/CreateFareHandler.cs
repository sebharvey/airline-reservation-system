using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.CreateFare;

public sealed class CreateFareHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<CreateFareHandler> _logger;

    public CreateFareHandler(IOfferRepository repository, ILogger<CreateFareHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Fare> HandleAsync(CreateFareCommand command, CancellationToken ct = default)
    {
        var inventory = await _repository.GetInventoryByIdAsync(command.InventoryId, ct)
            ?? throw new KeyNotFoundException($"Inventory {command.InventoryId} not found.");

        var existingFare = await _repository.GetFareAsync(command.InventoryId, command.FareBasisCode, ct);
        if (existingFare is not null)
            throw new InvalidOperationException($"Fare {command.FareBasisCode} already exists for inventory {command.InventoryId}.");

        var bookingClass = command.BookingClass ?? command.CabinCode;

        var fare = Fare.Create(
            command.InventoryId, command.FareBasisCode, command.FareFamily,
            command.CabinCode, bookingClass, command.CurrencyCode,
            command.BaseFareAmount, command.TaxAmount,
            command.IsRefundable, command.IsChangeable,
            command.ChangeFeeAmount, command.CancellationFeeAmount,
            command.PointsPrice, command.PointsTaxes,
            DateTimeOffset.Parse(command.ValidFrom), DateTimeOffset.Parse(command.ValidTo));

        await _repository.CreateFareAsync(fare, ct);

        _logger.LogInformation("Created Fare {FareId} ({FareBasisCode}) for inventory {InventoryId}",
            fare.FareId, command.FareBasisCode, command.InventoryId);

        return fare;
    }
}
