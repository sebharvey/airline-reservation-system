using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UpdateHoldSeat;

public sealed record UpdateHoldSeatCommand(Guid InventoryId, Guid OrderId, string PassengerId, string SeatNumber);

public sealed class UpdateHoldSeatHandler
{
    private readonly IOfferRepository _repository;

    public UpdateHoldSeatHandler(IOfferRepository repository) => _repository = repository;

    public Task<bool> HandleAsync(UpdateHoldSeatCommand command, CancellationToken ct = default)
        => _repository.UpdateHoldSeatAsync(command.InventoryId, command.OrderId, command.PassengerId, command.SeatNumber, ct);
}
