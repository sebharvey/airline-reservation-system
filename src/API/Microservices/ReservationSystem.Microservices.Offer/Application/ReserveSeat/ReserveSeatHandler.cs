using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.ReserveSeat;

public sealed class ReserveSeatHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<ReserveSeatHandler> _logger;

    public ReserveSeatHandler(IOfferRepository repository, ILogger<ReserveSeatHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<string>> HandleAsync(ReserveSeatCommand command, CancellationToken ct = default)
    {
        var inventory = await _repository.GetInventoryByIdAsync(command.FlightId, ct)
            ?? throw new KeyNotFoundException($"Inventory {command.FlightId} not found.");

        var existing = await _repository.GetSeatReservationsAsync(command.FlightId, ct);
        var occupiedSeats = existing.Select(r => r.SeatNumber).ToHashSet();

        var conflicts = command.SeatNumbers.Where(s => occupiedSeats.Contains(s)).ToList();
        if (conflicts.Count > 0)
            throw new InvalidOperationException($"Seats already reserved: {string.Join(", ", conflicts)}");

        await _repository.CreateSeatReservationsAsync(command.FlightId, command.BasketId, command.SeatNumbers, ct);

        _logger.LogInformation("Reserved seats {Seats} on flight {FlightId} for basket {BasketId}",
            string.Join(", ", command.SeatNumbers), command.FlightId, command.BasketId);

        return command.SeatNumbers;
    }
}
