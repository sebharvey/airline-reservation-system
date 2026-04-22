using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Application.ReleaseInventory;
using ReservationSystem.Microservices.Offer.Application.SellInventory;

namespace ReservationSystem.Microservices.Offer.Application.RebookInventory;

public sealed class RebookInventoryHandler
{
    private readonly SellInventoryHandler _sellHandler;
    private readonly ReleaseInventoryHandler _releaseHandler;
    private readonly ILogger<RebookInventoryHandler> _logger;

    public RebookInventoryHandler(
        SellInventoryHandler sellHandler,
        ReleaseInventoryHandler releaseHandler,
        ILogger<RebookInventoryHandler> logger)
    {
        _sellHandler = sellHandler;
        _releaseHandler = releaseHandler;
        _logger = logger;
    }

    public async Task HandleAsync(RebookInventoryCommand command, CancellationToken ct = default)
    {
        await _sellHandler.HandleAsync(
            new SellInventoryCommand(command.ToItems.ToList(), command.OrderId), ct);

        await _releaseHandler.HandleAsync(
            new ReleaseInventoryCommand(command.FromInventoryId, command.FromCabinCode, command.OrderId, "Sold", null), ct);

        _logger.LogInformation(
            "Rebooked inventory for order {OrderId}: released {FromInventoryId}/{FromCabinCode}, sold {ToCount} replacement leg(s)",
            command.OrderId, command.FromInventoryId, command.FromCabinCode, command.ToItems.Count);
    }
}
