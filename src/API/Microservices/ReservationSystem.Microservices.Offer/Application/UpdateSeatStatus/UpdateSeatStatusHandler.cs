using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UpdateSeatStatus;

public sealed class UpdateSeatStatusHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<UpdateSeatStatusHandler> _logger;

    public UpdateSeatStatusHandler(IOfferRepository repository, ILogger<UpdateSeatStatusHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<int> HandleAsync(UpdateSeatStatusCommand command, CancellationToken ct = default)
    {
        _ = await _repository.GetInventoryByIdAsync(command.FlightId, ct)
            ?? throw new KeyNotFoundException($"Inventory {command.FlightId} not found.");

        foreach (var update in command.Updates)
        {
            await _repository.UpdateSeatStatusAsync(command.FlightId, update.SeatNumber, update.Status, ct);
        }

        _logger.LogInformation("Updated {Count} seat statuses on flight {FlightId}",
            command.Updates.Count, command.FlightId);

        return command.Updates.Count;
    }
}
