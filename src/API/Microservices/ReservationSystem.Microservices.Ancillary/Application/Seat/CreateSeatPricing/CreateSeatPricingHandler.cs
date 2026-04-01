using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Seat;
using ReservationSystem.Microservices.Ancillary.Domain.Repositories.Seat;

namespace ReservationSystem.Microservices.Ancillary.Application.Seat.CreateSeatPricing;

/// <summary>
/// Handles the <see cref="CreateSeatPricingCommand"/>.
/// Creates and persists a new seat pricing rule.
/// </summary>
public sealed class CreateSeatPricingHandler
{
    private readonly ISeatPricingRepository _repository;
    private readonly ILogger<CreateSeatPricingHandler> _logger;

    public CreateSeatPricingHandler(
        ISeatPricingRepository repository,
        ILogger<CreateSeatPricingHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SeatPricing> HandleAsync(CreateSeatPricingCommand command, CancellationToken cancellationToken = default)
    {
        var entity = SeatPricing.Create(command.CabinCode, command.SeatPosition, command.CurrencyCode, command.Price, command.ValidFrom, command.ValidTo);
        var created = await _repository.CreateAsync(entity, cancellationToken);
        _logger.LogInformation("Created SeatPricing {SeatPricingId} for cabin {CabinCode}", created.SeatPricingId, command.CabinCode);
        return created;
    }
}
